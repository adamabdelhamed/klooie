using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace klooie;
internal sealed class SubscriberCollection
{
    private const int MinRetainedSubscriberCapacity = 8;
    private const int MinRetainedPendingAddCapacity = 4;

    [ThreadStatic]
    private static Stack<SubscriberCollection> lightweightCollectionPool;

    internal int ThreadId;
    private bool hasSingleSubscriber;
    private Subscriber singleSubscriber;
    private List<Subscriber>? subscribers;
    private bool hasSinglePendingAdd;
    private PendingAdd singlePendingAdd;
    private List<PendingAdd>? newSubscribersForNextNotificationCycle;
    private int notificationDepth;

    public int Count => EnsureCoherentState();
    private SubscriberCollection() => ThreadId = Thread.CurrentThread.ManagedThreadId;

    public static SubscriberCollection Create()
    {
        lightweightCollectionPool ??= new Stack<SubscriberCollection>(100);
        return lightweightCollectionPool.Count > 0 ? lightweightCollectionPool.Pop() : new SubscriberCollection();
    }

    private static Subscriber CreateSubscriber(ISubscription sub)
    {
        return new Subscriber { Subscription = sub, Lease = sub.Lease };
    }

    public void Subscribe(ISubscription subscription)
    {
        if (PrepareToSubscribe(subscription) == false) return;
        AddPending(new PendingAdd { Entry = CreateSubscriber(subscription), Priority = false });
    }

    public void SubscribeWithPriority(ISubscription subscription)
    {
        if (PrepareToSubscribe(subscription) == false) return;
        AddPending(new PendingAdd { Entry = CreateSubscriber(subscription), Priority = true });
    }

    private bool PrepareToSubscribe(ISubscription subscription)
    {
        AssertThreadForSubscription(subscription);
        if (subscription.IsStillValid(subscription.Lease) == false)
        {
            subscription.TryDispose(subscription.Lease, "external/klooie/src/klooie/Observability/SubscriberCollection.cs:54");
            return false;
        }

        if (notificationDepth == 0 && PendingCount > 0) EnsureCoherentState();
        return true;
    }

    private int PendingCount => (hasSinglePendingAdd ? 1 : 0) + (newSubscribersForNextNotificationCycle?.Count ?? 0);

    private int SubscriberCount => (hasSingleSubscriber ? 1 : 0) + (subscribers?.Count ?? 0);

    private void AddPending(PendingAdd pendingAdd)
    {
        if (newSubscribersForNextNotificationCycle == null && hasSinglePendingAdd == false)
        {
            singlePendingAdd = pendingAdd;
            hasSinglePendingAdd = true;
            return;
        }

        newSubscribersForNextNotificationCycle ??= new List<PendingAdd>(4);
        if (hasSinglePendingAdd)
        {
            newSubscribersForNextNotificationCycle.Add(singlePendingAdd);
            singlePendingAdd = default;
            hasSinglePendingAdd = false;
        }

        newSubscribersForNextNotificationCycle.Add(pendingAdd);
    }

    private void AddSubscriber(Subscriber entry, bool priority)
    {
        if (subscribers == null && hasSingleSubscriber == false)
        {
            singleSubscriber = entry;
            hasSingleSubscriber = true;
            return;
        }

        subscribers ??= new List<Subscriber>(4);
        if (hasSingleSubscriber)
        {
            subscribers.Add(singleSubscriber);
            singleSubscriber = default;
            hasSingleSubscriber = false;
        }

        if (priority)
        {
            subscribers.Insert(0, entry);
        }
        else
        {
            subscribers.Add(entry);
        }
    }

    private void RemoveSingleSubscriber()
    {
        singleSubscriber.Subscription?.TryDispose(singleSubscriber.Lease, "external/klooie/src/klooie/Observability/SubscriberCollection.cs:99");
        singleSubscriber = default;
        hasSingleSubscriber = false;
    }

    private void RemoveSubscriberAt(int i)
    {
        var entry = subscribers![i];
        subscribers.RemoveAt(i);
        entry.Subscription?.TryDispose(entry.Lease, "external/klooie/src/klooie/Observability/SubscriberCollection.cs:69");
    }

    public void Notify<T>(T arg)
    {
#if DEBUG
        // Surprisingly, this check is expensive and shows up in profiles, so we only do it in debug builds.
        // It is dangerous leaving this out of the release build, but we have to balance safety vs performance here.
        AssertThreadForNotify();
#endif
        EnsureCoherentState();
        notificationDepth++;
        try
        {
            if (hasSingleSubscriber)
            {
                (singleSubscriber.Subscription as ArgsSubscription<T>)?.SetArgs(arg); // setting args has no side effect. It just prepares the subscription for notification.
                singleSubscriber.Subscription?.Notify();
                if (singleSubscriber.Subscription?.IsStillValid(singleSubscriber.Lease) != true) RemoveSingleSubscriber();
                return;
            }

            if (subscribers == null) return;
            for (var i = 0; i < subscribers.Count; i++)
            {
                var entry = subscribers[i];
                (entry.Subscription as ArgsSubscription<T>)?.SetArgs(arg); // setting args has no side effect. It just prepares the subscription for notification.
                entry.Subscription?.Notify();
            }
        }
        finally
        {
            notificationDepth--;
        }
    }

    public void Notify()
    {
#if DEBUG
        // Surprisingly, this check is expensive and shows up in profiles, so we only do it in debug builds.
        // It is dangerous leaving this out of the release build, but we have to balance safety vs performance here.
        AssertThreadForNotify();
#endif
        EnsureCoherentState();
        notificationDepth++;
        try
        {
            if (hasSingleSubscriber)
            {
                singleSubscriber.Subscription?.Notify();
                if (singleSubscriber.Subscription?.IsStillValid(singleSubscriber.Lease) != true) RemoveSingleSubscriber();
                return;
            }

            if (subscribers == null) return;
            for (var i = 0; i < subscribers.Count; i++)
            {
                var entry = subscribers[i];
                entry.Subscription?.Notify();
            }
        }
        finally
        {
            notificationDepth--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int EnsureCoherentState()
    {
        var changed = false;
        if (hasSinglePendingAdd)
        {
            AddSubscriber(singlePendingAdd.Entry, singlePendingAdd.Priority);
            singlePendingAdd = default;
            hasSinglePendingAdd = false;
            changed = true;
        }

        if (newSubscribersForNextNotificationCycle?.Count > 0)
        {
            subscribers?.EnsureCapacity(SubscriberCount + newSubscribersForNextNotificationCycle.Count);
            for (var i = 0; i < newSubscribersForNextNotificationCycle.Count; i++)
            {
                AddSubscriber(newSubscribersForNextNotificationCycle[i].Entry, newSubscribersForNextNotificationCycle[i].Priority);
            }
            newSubscribersForNextNotificationCycle.Clear();
            changed = true;
        }

        if (hasSingleSubscriber && singleSubscriber.Subscription?.IsStillValid(singleSubscriber.Lease) != true)
        {
            RemoveSingleSubscriber();
            changed = true;
        }

        if (subscribers != null)
        {
            for (var i = subscribers.Count - 1; i >= 0; i--)
            {
                var entry = subscribers[i];
                if (entry.Subscription?.IsStillValid(entry.Lease) == true) continue;

                RemoveSubscriberAt(i);
                changed = true;
            }
        }

        if (changed) TrimOversizedLists();
        return SubscriberCount;
    }

    public void Dispose()
    {
        if (ThreadId != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("Cannot dispose SubscriberCollection from a different thread");
        notificationDepth = 0;

        if (hasSinglePendingAdd)
        {
            singlePendingAdd.Entry.Subscription?.TryDispose(singlePendingAdd.Entry.Lease, "external/klooie/src/klooie/Observability/SubscriberCollection.cs:196");
            singlePendingAdd = default;
            hasSinglePendingAdd = false;
        }

        if (newSubscribersForNextNotificationCycle != null)
        {
            for(var i = newSubscribersForNextNotificationCycle.Count - 1; i >= 0; i--)
            {
                var entry = newSubscribersForNextNotificationCycle[i].Entry;
                entry.Subscription?.TryDispose(entry.Lease, "external/klooie/src/klooie/Observability/SubscriberCollection.cs:115");
                newSubscribersForNextNotificationCycle.RemoveAt(i);
            }
        }

        if (hasSingleSubscriber)
        {
            RemoveSingleSubscriber();
        }

        if (subscribers != null)
        {
            for (int i = subscribers.Count - 1; i >= 0; i--)
            {
                RemoveSubscriberAt(i);
            }
        }

        TrimOversizedLists(force: true);
        lightweightCollectionPool.Push(this);
    }

    private void TrimOversizedLists(bool force = false)
    {
        if (subscribers != null) TrimOversizedList(subscribers, MinRetainedSubscriberCapacity, force);
        if (newSubscribersForNextNotificationCycle != null) TrimOversizedList(newSubscribersForNextNotificationCycle, MinRetainedPendingAddCapacity, force);
    }

    private static void TrimOversizedList<T>(List<T> list, int minRetainedCapacity, bool force)
    {
        if (list.Capacity <= minRetainedCapacity) return;

        var targetCapacity = Math.Max(minRetainedCapacity, list.Count * 2);
        if (force == false && list.Capacity <= targetCapacity * 2) return;
        list.Capacity = targetCapacity;
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

    private struct Subscriber
    {
        public ISubscription? Subscription;
        public int Lease;
    }
}
