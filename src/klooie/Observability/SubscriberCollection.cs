using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

internal interface ISubscription
{
    int Lease { get; }
    bool IsStillValid(int lease);
    void Notify();
}
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
    public void Track(ISubscription subscription)
    {
        if (subscription == null) throw new ArgumentNullException(nameof(subscription));
        var state = SubscriptionEntry.Create(subscriptions, subscription);
        subscriptions.Add(state);
    }

    public void TrackWithPriority(ISubscription subscription)
    {
        if (subscription == null) throw new ArgumentNullException(nameof(subscription));
        var state = SubscriptionEntry.Create(subscriptions, subscription);
        subscriptions.Insert(0, state);
    }

    // implement indexer
    public ISubscription this[int index]
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
        public ISubscription ToTrack { get; private set; }
        public int Lease { get; private set; }

        public static SubscriptionEntry Create(List<SubscriptionEntry> list, ISubscription toTrack)
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

 