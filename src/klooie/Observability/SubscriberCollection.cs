using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace klooie;
internal class SubscriberCollection
{
    [ThreadStatic]
    private static Stack<SubscriberCollection> collectionLightPool;
    [ThreadStatic]
    private static Stack<SubscriberEntry> entryLightPool;

    private const int SmallNotificationBufferSize = 64;

    // Per-thread stack of reusable small buffers for re-entrant notifications
    [ThreadStatic]
    private static Stack<ISubscription[]> _smallBufferStack;

    internal int ThreadId;

    private List<SubscriberEntry> entries = new List<SubscriberEntry>(100);
    public int Count => RefreshAndCountActiveEntries();

    public static SubscriberCollection Create()
    {
        collectionLightPool = collectionLightPool ?? new Stack<SubscriberCollection>(100);
        var ret = collectionLightPool.Count > 0 ? collectionLightPool.Pop() : new SubscriberCollection();
        ret.ThreadId = Thread.CurrentThread.ManagedThreadId;
        return ret;
    }

    private static SubscriberEntry GetEntry()
    {
        entryLightPool = entryLightPool ?? new Stack<SubscriberEntry>(100);
        var ret = entryLightPool.Count > 0 ? entryLightPool.Pop() : new SubscriberEntry();
        ret.ThreadId = Thread.CurrentThread.ManagedThreadId;
        return ret;
    }

    private SubscriberCollection() { }

    public void Track(ISubscription subscription)
    {
        if (((Recyclable)subscription).ThreadId != this.ThreadId)
            throw new InvalidOperationException("Cannot track a subscription that was created on a different thread.");

        var entry = GetEntry();
        if (((Recyclable)subscription).ThreadId != entry.ThreadId)
            throw new InvalidOperationException("Cannot track a subscription that was created on a different thread.");

        entry.Subscription = subscription;
        entry.Lease = subscription.Lease;
        entries.Add(entry);
    }

    public void TrackWithPriority(ISubscription subscription)
    {
        if (((Recyclable)subscription).ThreadId != this.ThreadId)
            throw new InvalidOperationException("Cannot track a subscription that was created on a different thread.");

        var entry = GetEntry();
        if (((Recyclable)subscription).ThreadId != entry.ThreadId)
            throw new InvalidOperationException("Cannot track a subscription that was created on a different thread.");

        entry.Subscription = subscription;
        entry.Lease = subscription.Lease;
        entries.Insert(0, entry);
    }

    private void DisposeEntryAt(int i)
    {
        var entry = entries[i];
        entry.Subscription = null;
        entry.Lease = 0;
        entries.RemoveAt(i);
        entryLightPool = entryLightPool ?? new Stack<SubscriberEntry>(100);
        entryLightPool.Push(entry);
    }

    private int RefreshAndCountActiveEntries()
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Subscription.IsStillValid(entry.Lease) == false)
            {
                DisposeEntryAt(i);
            }
        }
        return entries.Count;
    }

    public void Dispose()
    {
        if (ThreadId != Thread.CurrentThread.ManagedThreadId)
            throw new InvalidOperationException("Cannot dispose SubscriberCollection from a different thread");

        while (entries.Count > 0)
        {
            var entry = entries[0];
            if (entry.Subscription.IsStillValid(entry.Lease))
            {
                entry.Subscription.Dispose();
            }
            DisposeEntryAt(0);
        }
        collectionLightPool = collectionLightPool ?? new Stack<SubscriberCollection>(100);
        collectionLightPool.Push(this);
    }

    public void Notify<T>(T arg, DelayState? dependencies = null)
    {
        // Phase 1: set args on live entries (preserves original semantics)
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Subscription is ArgsSubscription<T> sub)
            {
                sub.Args = arg;
            }
        }
        // Phase 2: notify via snapshot
        Notify(dependencies);
    }

    public void Notify(DelayState? dependencies = null)
    {
        RefreshAndCountActiveEntries();
        if (entries.Count == 0) return;
        var subscriberCount = entries.Count;

        // Fast path: single subscriber
        if (subscriberCount == 1)
        {
            Notify(entries[0].Subscription, dependencies);
            return;
        }

        var fromStack = false;
        ISubscription[] buffer = AcquireBuffer(subscriberCount, out fromStack);

        try
        {
            // Copy the current subscribers into the buffer
            for (var i = 0; i < subscriberCount; i++)
            {
                buffer[i] = entries[i].Subscription;
            }

            // Notify
            for (var i = 0; i < subscriberCount; i++)
            {
                Notify(buffer[i], dependencies);
            }
        }
        finally
        {
            ReleaseBuffer(buffer, fromStack);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ISubscription[] AcquireBuffer(int needed, out bool fromStack)
    {
        if (needed <= SmallNotificationBufferSize)
        {
            fromStack = true;
            _smallBufferStack ??= new Stack<ISubscription[]>(10);
           return _smallBufferStack.Count > 0 ? _smallBufferStack.Pop() : new ISubscription[SmallNotificationBufferSize];
        }
        fromStack = false;
        return ArrayPool<ISubscription>.Shared.Rent(needed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReleaseBuffer(ISubscription[] buffer, bool fromStack)
    {
        if (fromStack)
        {
            _smallBufferStack.Push(buffer);
        }
        else
        {
            ArrayPool<ISubscription>.Shared.Return(buffer);
        }
    }

    private static void Notify(ISubscription sub, DelayState? dependencies)
    {
        if (dependencies?.AreAllDependenciesValid == false) return;
        sub.Notify();
    }

    private class SubscriberEntry
    {
        internal int ThreadId;
        public ISubscription Subscription { get; set; }
        public int Lease { get; set; }
    }
}
