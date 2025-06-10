using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class SubscriptionListPool
{
#if DEBUG
    public static int Created { get; private set; }
    public static int Rented { get; private set; }
    public static int Returned { get; private set; }
    public static int AllocationsSaved => Rented - Created;

#endif
    private static readonly ConcurrentBag<List<Subscription>> _pool = new ConcurrentBag<List<Subscription>>();

    internal static List<Subscription> Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.TryTake(out var list))
        {
            list.Clear();
            return list;
        }

#if DEBUG
        Created++;
#endif

        return new List<Subscription>();
    }

    internal static void Return(List<Subscription>? subscriptions)
    {
        if (subscriptions == null) return;
#if DEBUG
        Returned++;
#endif

        for (var i = 0; i < subscriptions.Count; i++)
        {
            if (subscriptions[i].IsStillValid(subscriptions[i].CurrentVersion))
            {
                subscriptions[i].TryDispose();
                i--;
            }
        }
        subscriptions.Clear();
        _pool.Add(subscriptions);
    }
}