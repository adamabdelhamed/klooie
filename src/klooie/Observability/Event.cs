namespace klooie;
/// <summary>
/// A lifetime aware event
/// </summary>
public sealed class Event : Recyclable
{
    internal List<Subscription>? eventSubscribers;

    /// <summary>
    /// returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => eventSubscribers?.Count > 0;

    /// <summary>
    /// Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire() => NotificationBufferPool.Notify(eventSubscribers);

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    /// when the lifetime associated with the given lifetime manager is disposed.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    public void Subscribe(Action handler, ILifetime lifetimeManager)
    {
        eventSubscribers ??= SubscriptionListPool.Rent();
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Callback = handler;
        subscription.Lifetime = lifetimeManager;
        subscription.Subscribers = eventSubscribers;
        eventSubscribers.Add(subscription);
        lifetimeManager.OnDisposed(subscription);
    }

    // NEW OR MODIFIED CODE: Overload that accepts a scope object
    /// <summary>
    /// Subscribes to this event such that the given handler will be called (with a provided scope) when the event fires.
    /// </summary>
    /// <typeparam name="TScope">The type of the scope object</typeparam>
    /// <param name="scope">A scope object that the handler can use as its state</param>
    /// <param name="handler">A callback that accepts the scope object</param>
    /// <param name="lifetimeManager">The lifetime manager</param>
    public void Subscribe<TScope>(TScope scope, Action<object> handler, ILifetime lifetimeManager)
    {
        eventSubscribers ??= SubscriptionListPool.Rent();
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Scope = scope;
        subscription.ScopedCallback = handler;
        subscription.Lifetime = lifetimeManager;
        subscription.Subscribers = eventSubscribers;
        eventSubscribers.Add(subscription);
        lifetimeManager.OnDisposed(subscription);
    }

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. 
    /// This subscription will be notified before all other subscriptions.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager</param>
    public void SubscribeWithPriority(Action handler, ILifetime lifetimeManager)
    {
        eventSubscribers ??= SubscriptionListPool.Rent();
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Callback = handler;
        subscription.Lifetime = lifetimeManager;
        subscription.Subscribers = eventSubscribers;
        eventSubscribers.Insert(0, subscription);
        lifetimeManager.OnDisposed(subscription);
    }

    /// <summary>
    /// Subscribes to this event with a scope object and callback, notified before other subscriptions.
    /// </summary>
    public void SubscribeWithPriority<TScope>(TScope scope, Action<object> handler, ILifetime lifetimeManager)
    {
        eventSubscribers ??= SubscriptionListPool.Rent();
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Scope = scope;
        subscription.ScopedCallback = handler;
        subscription.Lifetime = lifetimeManager;
        subscription.Subscribers = eventSubscribers;
        eventSubscribers.Insert(0, subscription);
        lifetimeManager.OnDisposed(subscription);
    }

    /// <summary>
    /// calls the callback now and subscribes to the event
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lifetimeManager">the lifetime of the subscription</param>
    public void Sync(Action handler, ILifetime lifetimeManager)
    {
        handler();
        Subscribe(handler, lifetimeManager);
    }

    /// <summary>
    /// Calls the callback now and subscribes to the event with a scope object.
    /// </summary>
    public void Sync<TScope>(TScope scope, Action<object> handler, ILifetime lifetimeManager)
    {
        handler(scope);
        Subscribe(scope, handler, lifetimeManager);
    }

    /// <summary>
    /// calls the callback now and subscribes to the event. 
    /// This subscription will be notified before all other subscriptions.
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lifetimeManager">the lifetime of the subscription</param>
    public void SyncWithPriority(Action handler, ILifetime lifetimeManager)
    {
        handler();
        SubscribeWithPriority(handler, lifetimeManager);
    }

    // NEW OR MODIFIED CODE: Overload that accepts a scope object (sync with priority)
    /// <summary>
    /// Calls the callback now and subscribes to the event with a scope object, 
    /// notified before all other subscriptions.
    /// </summary>
    public void SyncWithPriority<TScope>(TScope scope, Action<object> handler, ILifetime lifetimeManager)
    {
        handler(scope);
        SubscribeWithPriority(scope, handler, lifetimeManager);
    }

    /// <summary>
    /// Subscribes to the event for one notification and then immediately unsubscribes so your callback will only be called at most once
    /// </summary>
    /// <param name="handler">The action to run when the event fires</param>
    public void SubscribeOnce(Action handler)
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        Subscribe(handler, lt);
        Subscribe(lt, DisposeStatic, lt);
    }

    public static void DisposeStatic(object lt) => ((Recyclable)lt).Dispose();

    // NEW OR MODIFIED CODE: Overload that accepts a scope object for one notification
    public void SubscribeOnce<TScope>(TScope scope, Action<object> handler)
    {
        Action wrappedAction = null;
        var lt = DefaultRecyclablePool.Instance.Rent();
        wrappedAction = () =>
        {
            try
            {
                handler(scope);
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
    public Recyclable CreateNextFireLifetime()
    {
        var lifetime = DefaultRecyclablePool.Instance.Rent();
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
        this.SubscribeOnce(tcs,(tcsObj) => (tcsObj as TaskCompletionSource).SetResult());
        return tcs.Task;
    }

    protected override void OnReturn()
    {
        if (eventSubscribers != null)
        {
            SubscriptionListPool.Return(eventSubscribers);
            eventSubscribers = null;
        }
    }
}

public interface IEventT
{
    public Stack<object> args { get; }
}

/// <summary>
/// An event that has an argument of the given type
/// </summary>
/// <typeparam name="T">the argument type</typeparam>
public class Event<T> : Recyclable, IEventT
{
    private Event? innerEvent;
    public Stack<object> args { get; } = new Stack<object>(); // because notifications can be re-entrant

    public bool HasSubscriptions => innerEvent?.HasSubscriptions == true;

    /// <summary>
    /// Subscribes for the given lifetime.
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lt">the lifetime</param>
    public void Subscribe(Action<T> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        innerEvent.Subscribe(() => handler((T)args.Peek()), lt);
    }

    // -----------------------------------------------
    /// <summary>
    /// Subscribes for the given lifetime, passing both a scope and the event argument.
    /// This avoids capturing local variables if you use a static method.
    /// </summary>
    public void Subscribe<TScope>(TScope scope, Action<object, object> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        innerEvent.eventSubscribers ??= SubscriptionListPool.Rent(); 
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Lifetime = lt;
        subscription.ScopedCallback = StaticCallback;
        subscription.TScopedCallback = handler;
        subscription.Scope = subscription;
        subscription.TScope = scope;
        subscription.eventT = this;
        subscription.Subscribers = innerEvent.eventSubscribers;
        innerEvent.eventSubscribers.Add(subscription);
        lt.OnDisposed(subscription);

    }

    private static void StaticCallback(object me)
    {
        var sub = (Subscription)me;
        sub.TScopedCallback.Invoke(sub.TScope, sub.eventT.args.Peek());
    }


    /// <summary>
    /// Subscribes with priority for the given lifetime.
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lt">the lifetime</param>
    public void SubscribeWithPriority(Action<T> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        innerEvent.SubscribeWithPriority(() => handler((T)args.Peek()), lt);
    }

    // -----------------------------------------------
    //  NEW: Overload that accepts a scope object
    // -----------------------------------------------
    /// <summary>
    /// Subscribes with priority for the given lifetime, passing both a scope and the event argument.
    /// </summary>
    public void SubscribeWithPriority<TScope>(TScope scope, Action<TScope, T> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        innerEvent.SubscribeWithPriority(scope, s => handler((TScope)s!, (T)args.Peek()), lt);
    }

    /// <summary>
    /// Subscribes once for the given callback.
    /// </summary>
    /// <param name="handler">the callback</param>
    public void SubscribeOnce(Action<T> handler)
    {
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        innerEvent.SubscribeOnce(() => handler((T)args.Peek()));
    }

    // -----------------------------------------------
    //  NEW: Overload that accepts a scope object
    // -----------------------------------------------
    /// <summary>
    /// Subscribes once with the given scope and callback, then unsubscribes immediately.
    /// </summary>
    public void SubscribeOnce<TScope>(TScope scope, Action<TScope, T> handler)
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        // Wrap the callback so we can dispose once it's called
        Action wrappedAction = null;
        wrappedAction = () =>
        {
            try
            {
                handler(scope, (T)args.Peek());
            }
            finally
            {
                lt.Dispose();
            }
        };

        // Now subscribe our wrapped callback
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        innerEvent.Subscribe(wrappedAction, lt);
    }

    /// <summary>
    /// Fires the event and passes the given args to subscribers.
    /// </summary>
    /// <param name="args">The event argument</param>
    public void Fire(T args)
    {
        if (innerEvent == null) return;

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
    public Recyclable CreateNextFireLifetime()
    {
        innerEvent = innerEvent ?? EventPool.Instance.Rent();
        return innerEvent.CreateNextFireLifetime();
    }

    /// <summary>
    /// Creates a task that completes the next time this event fires
    /// </summary>
    /// <returns>a task</returns>
    public Task<T> CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource<T>();
        this.SubscribeOnce(tcs.SetResult);
        return tcs.Task;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        innerEvent?.Dispose();
        innerEvent = null;
    }
}

public class EventPool<T> : RecycleablePool<Event<T>>  
{
    private static EventPool<T> instance;
    public static EventPool<T> Instance => instance ??= new EventPool<T>();
    public override Event<T> Factory() => new Event<T>();
}