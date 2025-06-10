using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal class SubscriberCollection
{
    public static SubscriberCollection Create() => new SubscriberCollection();

    private List<SubscriptionEntry> subscriptions = new List<SubscriptionEntry>();
    public int Count
    {
        get
        {
            UpdateList();
            return subscriptions.Count;
        }
    }
    public void Track(Subscription subscription)
    {
        if (subscription == null) throw new ArgumentNullException(nameof(subscription));
        var state = SubscriptionEntry.Create(subscriptions, subscription);
        subscriptions.Add(state);
    }

    public void TrackWithPriority(Subscription subscription)
    {
        if (subscription == null) throw new ArgumentNullException(nameof(subscription));
        var state = SubscriptionEntry.Create(subscriptions, subscription);
        subscriptions.Insert(0, state);
    }

    // implement indexer
    public Subscription this[int index]
    {
        get => subscriptions[index].ToTrack;
    }

    private void Untrack(object obj)
    {
        var state = (SubscriptionEntry)obj;
        state.List.Remove(state);
        state.Clear();
    }

    public void UpdateList()
    {
        for(var i = subscriptions.Count - 1; i >= 0; i--)
        {
            var entry = subscriptions[i];
            if (entry.ToTrack.IsStillValid(entry.Lease) == false)
            {
                Untrack(entry);
            }
        }
    }

    public void Clear()
    {
        while (subscriptions.Count > 0)
        {
            Untrack(subscriptions[0]);
        }
    }

    private class SubscriptionEntry
    {
        public List<SubscriptionEntry> List { get; private set; }
        public Subscription ToTrack { get; private set; }
        public int Lease { get; private set; }

        public static SubscriptionEntry Create(List<SubscriptionEntry> list, Subscription toTrack)
        {
            var ret = new SubscriptionEntry();
            ret.List = list;
            ret.ToTrack = toTrack;
            ret.Lease = toTrack.Lease;
            return ret;
        }

        public void Clear()
        {
            List = null!;
            ToTrack = null!;
            Lease = default;
        }
    }
}

 