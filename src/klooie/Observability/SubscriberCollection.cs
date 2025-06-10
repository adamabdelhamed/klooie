using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace klooie;
internal class SubscriberCollection
{
    private static Stack<SubscriberCollection> pool = new Stack<SubscriberCollection>();
    private List<(ISubscription Subscription, int Lease)> entries = new List<(ISubscription Subscription, int Lease)>();
    public int Count => RefreshAndCountActiveEntries();
    public static SubscriberCollection Create() => pool.Count > 0 ? pool.Pop() : new SubscriberCollection();
    private SubscriberCollection() { }

    public void Track(ISubscription subscription) => entries.Add((subscription, subscription.Lease));
    public void TrackWithPriority(ISubscription subscription) => entries.Insert(0, (subscription, subscription.Lease));

    private int RefreshAndCountActiveEntries()
    {
        for(var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Subscription.IsStillValid(entry.Lease) == false)
            {
                entries.RemoveAt(i);
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
            entries.RemoveAt(0);
        }
        pool.Push(this);
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
}

 