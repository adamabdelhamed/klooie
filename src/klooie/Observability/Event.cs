using System.Collections.Concurrent;

namespace klooie;
/// <summary>
/// A lifetime aware event
/// </summary>
public sealed class Event
{
    private List<Subscription>? subscribers;

    /// <summary>
    /// returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => subscribers?.Any() == true;
         
    /// <summary>
    /// Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire() => NotificationBufferPool.Notify(subscribers);

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    /// when the lifetime associated with the given lifetime manager is disposed.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    public void Subscribe(Action handler, ILifetimeManager lifetimeManager)
    {
        subscribers = subscribers ?? new List<Subscription>();
        var subscription = SubscriptionPool.Rent(handler, lifetimeManager);
        subscription.Subscribers = subscribers;
        subscribers.Add(subscription);
        lifetimeManager.OnDisposed(subscription);
    }

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    /// when the lifetime associated with the given lifetime manager is disposed. This subscription will be notified before all other subscriptions.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    public void SubscribeWithPriority(Action handler, ILifetimeManager lifetimeManager)
    {
        subscribers = subscribers ?? new List<Subscription>();
        var subscription = SubscriptionPool.Rent(handler, lifetimeManager);
        subscription.Subscribers = subscribers;
        subscribers.Insert(0, subscription);
        lifetimeManager.OnDisposed(subscription);
    }

    /// <summary>
    /// calls the callback now and subscribes to the event
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lifetimeManager">the lifetime of the subscription</param>
    public void Sync(Action handler, ILifetimeManager lifetimeManager)
    {
        handler();
        Subscribe(handler, lifetimeManager);
    }

    /// <summary>
    /// calls the callback now and subscribes to the event. This subscription will be notified before all other subscriptions.
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lifetimeManager">the lifetime of the subscription</param>
    public void SyncWithPriority(Action handler, ILifetimeManager lifetimeManager)
    {
        handler();
        SubscribeWithPriority(handler, lifetimeManager);
    }

    /// <summary>
    /// Subscribes to the event for one notification and then immediately unsubscribes so your callback will only be called at most once
    /// </summary>
    /// <param name="handler">The action to run when the event fires</param>
    public void SubscribeOnce(Action handler)
    {
        Action wrappedAction = null;
        var lt = new Lifetime();
        wrappedAction = () =>
        {
            try
            {
                handler();
            }
            finally
            {
                lt.Dispose();
            }
        };

        Subscribe(wrappedAction, lt);
    }


    /// <summary>
    /// Creates a lifetime that will end the next time this
    /// event fires
    /// </summary>
    /// <returns>a lifetime that will end the next time this event fires</returns>
    public Lifetime CreateNextFireLifetime()
    {
        var lifetime = new Lifetime();
        this.SubscribeOnce(lifetime.Dispose);
        return lifetime;
    }

    /// <summary>
    /// Creates a task that will complete the next time this event fires
    /// </summary>
    /// <returns>a task</returns>
    public Task CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource();
        this.SubscribeOnce(()=> tcs.SetResult());
        return tcs.Task;
    }

    internal void Reset()
    {
        subscribers?.Clear();
    }
}

/// <summary>
/// An event that has an argument of the given type
/// </summary>
/// <typeparam name="T">the argument type</typeparam>
public class Event<T>
{
    private Event innerEvent = new Event();
    private Stack<T> args = new Stack<T>(); // because notifications can be re-entrant

    /// <summary>
    /// Subscribes for the given lifetime
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lt">the lifetime</param>
    public void Subscribe(Action<T> handler, ILifetimeManager lt) => innerEvent.Subscribe(() => handler(args.Peek()), lt);

    public void SubscribeWithPriority(Action<T> handler, ILifetimeManager lt) => innerEvent.SubscribeWithPriority(() => handler(args.Peek()), lt);

    /// <summary>
    /// Subscribes for one notification
    /// </summary>
    /// <param name="handler">the callback</param>
    public void SubscribeOnce(Action<T> handler) => innerEvent.SubscribeOnce(() => handler(args.Peek()));

    /// <summary>
    /// Fires the event and passes the given args to subscribers
    /// </summary>
    /// <param name="args"></param>
    public void Fire(T args)
    {
        this.args.Push(args);
        try
        {
            innerEvent.Fire();
        }
        finally
        {
            this.args.Pop();
        }
    }

    /// <summary>
    /// Creates a lifetime that will end the next time this event fires
    /// </summary>
    /// <returns>a lifetime that will end the next time this event fires</returns>
    public Lifetime CreateNextFireLifetime() => innerEvent.CreateNextFireLifetime();

    /// <summary>
    /// Creates a task that completes the next time this event fires
    /// </summary>
    /// <returns>a task</returns>
    public Task<T> CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource<T>();
        this.SubscribeOnce(args => tcs.SetResult(args));
        return tcs.Task;
    }

    internal void Reset()
    {
        innerEvent.Reset();
    }
}

public static class EventPool
{
#if DEBUG
    public static int EventsCreated { get; private set; }
    public static int EventsRented { get; private set; }
    public static int EventsReturned { get; private set; }
    public static int AllocationsSaved => EventsRented - EventsCreated;

#endif
    private static readonly ConcurrentBag<Event> _pool = new ConcurrentBag<Event>();

    public static Event Rent()
    {
#if DEBUG
        EventsRented++;
#endif
        if (_pool.TryTake(out var evt))
        {
            return evt;
        }

#if DEBUG
        EventsCreated++;
#endif

        return new Event();
    }

    public static void Return(Event evt)
    {
#if DEBUG
        EventsReturned++;
#endif
        evt.Reset(); // Clear subscribers and any other state
        _pool.Add(evt);
    }
}


public static class EventPool<T>
{
#if DEBUG
    public static int EventsCreated { get; private set; }
    public static int EventsRented { get; private set; }
    public static int EventsReturned { get; private set; }
    public static int AllocationsSaved => EventsRented - EventsCreated;

#endif
    private static readonly ConcurrentBag<Event<T>> _pool = new ConcurrentBag<Event<T>>();

    public static Event<T> Rent()
    {
#if DEBUG
        EventsRented++;
#endif
        if (_pool.TryTake(out var evt))
        {
            return evt;
        }

#if DEBUG
        EventsCreated++;
#endif

        return new Event<T>();
    }

    public static void Return(Event<T> evt)
    {
#if DEBUG
        EventsReturned++;
#endif
        evt.Reset(); // Clear subscribers and any other state
        _pool.Add(evt);
    }
}

public static class SubscriptionPool
{
#if DEBUG
    public static int Created { get; private set; }
    public static int Rented { get; private set; }
    public static int Returned { get; private set; }
    public static int AllocationsSaved => Rented - Created;

#endif
    private static readonly ConcurrentBag<Subscription> _pool = new ConcurrentBag<Subscription>();

    internal static Subscription Rent(Action callback, ILifetimeManager lifetime)
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.TryTake(out var evt))
        {
            evt.Callback = callback;
            evt.Lifetime = lifetime;
            return evt;
        }

#if DEBUG
        Created++;
#endif

        return new Subscription() { Callback = callback, Lifetime = lifetime };
    }

    internal static void Return(Subscription subscription)
    {
#if DEBUG
        Returned++;
#endif
        subscription.Dispose();
        _pool.Add(subscription);
    }
}