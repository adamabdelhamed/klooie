using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace klooie;
internal sealed class SubscriberCollection
{
    [ThreadStatic]
    private static Stack<SubscriberCollection> lightweightCollectionPool;
    [ThreadStatic]
    private static Stack<Subscriber> lightweightSubscriberPool;

    internal int ThreadId;
    private readonly List<Subscriber> subscribers = new List<Subscriber>(100);
    private readonly List<PendingAdd> newSubscribersForNextNotificationCycle = new List<PendingAdd>(32);

    public int Count => EnsureCoherentState();
    private SubscriberCollection() => ThreadId = Thread.CurrentThread.ManagedThreadId;

    public static SubscriberCollection Create()
    {
        lightweightCollectionPool ??= new Stack<SubscriberCollection>(100);
        return lightweightCollectionPool.Count > 0 ? lightweightCollectionPool.Pop() : new SubscriberCollection();
    }

    private static Subscriber CreateSubscriber(ISubscription sub)
    {
        lightweightSubscriberPool ??= new Stack<Subscriber>(100);
        var ret = lightweightSubscriberPool.Count > 0 ? lightweightSubscriberPool.Pop() : new Subscriber();
        ret.Subscription = sub;
        ret.Lease = sub.Lease;
        return ret;
    }

    public void Subscribe(ISubscription subscription)
    {
        AssertThreadForSubscription(subscription);
        newSubscribersForNextNotificationCycle.Add(new PendingAdd { Entry = CreateSubscriber(subscription), Priority = false });
    }

    public void SubscribeWithPriority(ISubscription subscription)
    {
        AssertThreadForSubscription(subscription);
        newSubscribersForNextNotificationCycle.Add(new PendingAdd { Entry = CreateSubscriber(subscription), Priority = true });
    }

    private void RemoveEntryAt(int i)
    {
        var entry = subscribers[i];
        subscribers.RemoveAt(i);
        entry.Subscription = null;
        entry.Lease = 0;
        lightweightSubscriberPool.Push(entry);
    }

    public void Notify<T>(T arg)
    {
        AssertThreadForNotify();
        EnsureCoherentState();
        for (var i = 0; i < subscribers.Count; i++)
        {
            (subscribers[i].Subscription as ArgsSubscription<T>)?.SetArgs(arg); // setting args has no side effect. It just prepares the subscription for notification.
            subscribers[i].Subscription.Notify();
        }
    }

    public void Notify()
    {
        if (Thread.CurrentThread.ManagedThreadId != ThreadId) throw new InvalidOperationException("Cannot notify subscribers from a different thread than the one that created the SubscriberCollection");
        EnsureCoherentState();
        for (var i = 0; i < subscribers.Count; i++) subscribers[i].Subscription.Notify();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int EnsureCoherentState()
    {
        subscribers.EnsureCapacity(subscribers.Count + newSubscribersForNextNotificationCycle.Count);
        for (var i = 0; i < newSubscribersForNextNotificationCycle.Count; i++)
        {
            if (newSubscribersForNextNotificationCycle[i].Priority)
            {
                subscribers.Insert(0, newSubscribersForNextNotificationCycle[i].Entry);
            }
            else
            {
                subscribers.Add(newSubscribersForNextNotificationCycle[i].Entry);
            }
        }
        newSubscribersForNextNotificationCycle.Clear();

        for (var i = subscribers.Count - 1; i >= 0; i--)
        {
            var entry = subscribers[i];
            if (entry.Subscription.IsStillValid(entry.Lease) == false) RemoveEntryAt(i);
        }
        return subscribers.Count;
    }

    public void Dispose()
    {
        if (ThreadId != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Cannot dispose SubscriberCollection from a different thread");
        EnsureCoherentState();

        for (int i = subscribers.Count - 1; i >= 0; i--)
        {
            var entry = subscribers[i];
            entry.Subscription.TryDispose();
            RemoveEntryAt(i);
        }
        lightweightCollectionPool.Push(this);
    }

    private void AssertThreadForSubscription(ISubscription sub)
    {
        if (sub.ThreadId != ThreadId) throw new InvalidOperationException("Cannot track a subscription that was created on a different thread.");
        if (ThreadId != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Cannot modify SubscriberCollection from a different thread than the one that created it");
    }

    private void AssertThreadForNotify()
    {
        if (ThreadId != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Cannot notify subscribers from a different thread than the one that created the SubscriberCollection");
    }

    private struct PendingAdd
    {
        public Subscriber Entry;
        public bool Priority;
    }

    private sealed class Subscriber
    {
        public ISubscription Subscription;
        public int Lease;
        public Subscriber() { }
    }
}
