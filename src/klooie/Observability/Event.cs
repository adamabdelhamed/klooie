namespace klooie;
/// <summary>
/// A lifetime aware event
/// </summary>
public class Event : Recyclable
{
    private SubscriberCollection? subscribers;
    private SubscriberCollection Subscribers => subscribers ??= SubscriberCollection.Create();

    /// <summary>
    /// returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => subscribers == null ? false : subscribers.Count > 0;

    public int SubscriberCount => Subscribers?.Count ?? 0;

    private Event() { }

    private static readonly LazyPool<Event> Pool = new LazyPool<Event>(static () => new Event());
    public static Event Create() => Pool.Value.Rent();
    public static Event Create(out int lease) => Pool.Value.Rent(out lease);

    /// <summary>
    /// Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire() => Subscribers.Notify();

    public void Fire(DelayState dependencies) => subscribers?.Notify(dependencies);

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    /// when the lifetime associated with the given lifetime manager is disposed.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    public void Subscribe(Action handler, ILifetime lifetimeManager)
    {
        if(lifetimeManager == null) throw new ArgumentNullException(nameof(lifetimeManager), "Lifetime manager cannot be null");
        var subscription = ActionSubscription.Create(handler);
        Subscribers.Track(subscription);
        lifetimeManager.OnDisposed(LeaseHelper.Track(subscription), static lease => lease.UnTrackAndDispose());
    }

    // NEW OR MODIFIED CODE: Overload that accepts a scope object
    /// <summary>
    /// Subscribes to this event such that the given handler will be called (with a provided scope) when the event fires.
    /// </summary>
    /// <typeparam name="TScope">The type of the scope object</typeparam>
    /// <param name="scope">A scope object that the handler can use as its state</param>
    /// <param name="handler">A callback that accepts the scope object</param>
    /// <param name="lifetimeManager">The lifetime manager</param>
    public void Subscribe<TScope>(TScope scope, Action<TScope> handler, ILifetime lifetimeManager)
    {
        if (lifetimeManager == null) throw new ArgumentNullException(nameof(lifetimeManager), "Lifetime manager cannot be null");
        var subscription = ScopedSubscription<TScope>.Create(scope, handler);
        Subscribers.Track(subscription);
        lifetimeManager.OnDisposed(LeaseHelper.Track(subscription), static lease=> lease.UnTrackAndDispose());
    }

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. 
    /// This subscription will be notified before all other subscriptions.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager</param>
    public void SubscribeWithPriority(Action handler, ILifetime lifetimeManager)
    {
        if (lifetimeManager == null) throw new ArgumentNullException(nameof(lifetimeManager), "Lifetime manager cannot be null");
        var subscription = ActionSubscription.Create(handler);
        Subscribers.TrackWithPriority(subscription);
        lifetimeManager.OnDisposed(LeaseHelper.Track(subscription), static lease => lease.UnTrackAndDispose());
    }

    /// <summary>
    /// Subscribes to this event with a scope object and callback, notified before other subscriptions.
    /// </summary>
    public void SubscribeWithPriority<TScope>(TScope scope, Action<TScope> handler, ILifetime lifetimeManager)
    {
        if (lifetimeManager == null) throw new ArgumentNullException(nameof(lifetimeManager), "Lifetime manager cannot be null");
        var subscription = ScopedSubscription<TScope>.Create(scope, handler);
        Subscribers.TrackWithPriority(subscription);
        lifetimeManager.OnDisposed(LeaseHelper.Track(subscription), static lease => lease.UnTrackAndDispose());
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
    public void Sync<TScope>(TScope scope, Action<TScope> handler, ILifetime lifetimeManager)
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
        bool fired = false;
        Action wrappedHandler = null;
        wrappedHandler = () =>
        {
            if (fired) return;
            fired = true;
            try { handler(); }
            finally { lt.Dispose(); }
        };
        Subscribe(wrappedHandler, lt);
    }
    public void SubscribeOnce<TScope>(TScope scope, Action<TScope> handler)
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
        this.SubscribeOnce(lifetime, TryDisposeMe);
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
        base.OnReturn();
        subscribers?.Dispose();
        subscribers = null;
    }
}

