using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace klooie;
internal class SubscriberCollection
{
    private static Stack<SubscriberCollection> collectionLightPool = new Stack<SubscriberCollection>();
    private static Stack<SubscriberEntry> entryLightPool = new Stack<SubscriberEntry>();

    private List<SubscriberEntry> entries = new List<SubscriberEntry>();
    public int Count => RefreshAndCountActiveEntries();
    public static SubscriberCollection Create() => collectionLightPool.Count > 0 ? collectionLightPool.Pop() : new SubscriberCollection();

    private static SubscriberEntry GetEntry() => entryLightPool.Count > 0 ? entryLightPool.Pop() : new SubscriberEntry();

    private SubscriberCollection() { }

    public void Track(ISubscription subscription)
    {
        var entry = GetEntry();
        entry.Subscription = subscription;
        entry.Lease = subscription.Lease;
        entries.Add(entry);
    }
    public void TrackWithPriority(ISubscription subscription)
    {
        var entry = GetEntry();
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
        entryLightPool.Push(entry);
    }

    private int RefreshAndCountActiveEntries()
    {
        for(var i = entries.Count - 1; i >= 0; i--)
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
        while (entries.Count > 0)
        {
            var entry = entries[0];
            if(entry.Subscription.IsStillValid(entry.Lease))
            {
                entry.Subscription.Dispose();
            }
            DisposeEntryAt(0);
        }
        collectionLightPool.Push(this);
    }

    public void Notify<T>(T arg, DelayState? dependencies = null)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Subscription is ArgsSubscription<T> sub)
            {
                sub.Args = arg;
            }
        }
        Notify(dependencies);
    }

    public void Notify(DelayState? dependencies = null)
    {
        RefreshAndCountActiveEntries();
        if (entries.Count == 0) return;
        var subscriberCount = entries.Count;

        // Perf optimization for the very common 1 subscriber case since there's no need to copy the list,
        // rent a buffer, or bother with a loop and lifetime checks.
        if (subscriberCount == 1)
        {
            Notify(entries[0].Subscription, dependencies);
            return;
        }

        // buffer used to allow concurrent modification to subscribers during notification
        // buffer also reduces memory allocations insead of doing a ToArray() on the list
        var buffer = ArrayPool<ISubscription>.Shared.Rent(subscriberCount);
        try
        {
            // copy the current subscribers into the buffer
            for (var i = 0; i < subscriberCount; i++)
            {
                buffer[i] = entries[i].Subscription;
            }

            // now notify
            for (var i = 0; i < subscriberCount; i++)
            {
                var sub = buffer[i];
                Notify(sub, dependencies);
            }
        }
        finally
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
        public ISubscription Subscription { get; set; }
        public int Lease { get; set; }
    }
}

 