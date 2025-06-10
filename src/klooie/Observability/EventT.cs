namespace klooie;


public class Event<T> : Recyclable
{
    private readonly SubscriberCollection subscribers = SubscriberCollection.Create();

    /// <summary>
    /// returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => subscribers.Count > 0;

    public int SubscriberCount => subscribers.Count;

    private Event() { }

    private static readonly LazyPool<Event<T>> Pool = new LazyPool<Event<T>>(static () => new Event<T>());
    public static Event<T> Create() => Pool.Value.Rent();
    public static Event<T> Create(out int lease) => Pool.Value.Rent(out lease);

    /// <summary>
    /// Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire(T arg) => subscribers.Notify(arg);

    public void Fire(T arg, DelayState dependencies) => subscribers.Notify(arg, dependencies);


    public void Subscribe(Action<T> handler, ILifetime lifetimeManager)
    {
        var subscription = ArgsSubscription<T>.Create(handler);
        subscribers.Track(subscription);
        lifetimeManager.OnDisposed(subscription, TryDisposeMe);

    }
    public void Subscribe<TScope>(TScope scope, Action<TScope,T> handler, ILifetime lifetimeManager)
    {
        var subscription = ScopedArgsSubscription<TScope, T>.Create(scope, handler);
        subscribers.Track(subscription);
        lifetimeManager.OnDisposed(subscription, TryDisposeMe);
    }

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. 
    /// This subscription will be notified before all other subscriptions.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager</param>
    public void SubscribeWithPriority(Action<T> handler, ILifetime lifetimeManager)
    {
        var subscription = ArgsSubscription<T>.Create(handler);
        subscribers.TrackWithPriority(subscription);
        lifetimeManager.OnDisposed(subscription, TryDisposeMe);
    }

    /// <summary>
    /// Subscribes to this event with a scope object and callback, notified before other subscriptions.
    /// </summary>
    public void SubscribeWithPriority<TScope>(TScope scope, Action<TScope,T> handler, ILifetime lifetimeManager)
    {
        var subscription = ScopedArgsSubscription<TScope, T>.Create(scope, handler);
        subscribers.TrackWithPriority(subscription);
        lifetimeManager.OnDisposed(subscription, TryDisposeMe);
    }


   
    /// <summary>
    /// Subscribes to the event for one notification and then immediately unsubscribes so your callback will only be called at most once
    /// </summary>
    /// <param name="handler">The action to run when the event fires</param>
    public void SubscribeOnce(Action<T> handler)
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        bool fired = false;
        Action<T> wrappedHandler = null;
        wrappedHandler = (arg) =>
        {
            if (fired) return;
            fired = true;
            try { handler(arg); }
            finally { lt.Dispose(); }
        };
        Subscribe(wrappedHandler, lt);
    }
    public void SubscribeOnce<TScope>(TScope scope, Action<TScope,T> handler)
    {
        Action<T> wrappedAction = null;
        var lt = DefaultRecyclablePool.Instance.Rent();
        wrappedAction = (arg) =>
        {
            try
            {
                handler(scope, arg);
            }
            finally
            {
                lt.Dispose();
            }
        };

        Subscribe(wrappedAction, lt);
    }

    public Recyclable CreateNextFireLifetime()
    {
        var lifetime = DefaultRecyclablePool.Instance.Rent();
        this.SubscribeOnce(lifetime, TryDisposeMe);
        return lifetime;
    }

    private static void TryDisposeMe(Recyclable lt, T _) => Recyclable.TryDisposeMe(lt);

    /// <summary>
    /// Creates a task that will complete the next time this event fires
    /// </summary>
    /// <returns>a task</returns>
    public Task<T> CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource<T>();
        this.SubscribeOnce<TaskCompletionSource<T>>(tcs, static (TaskCompletionSource<T> tcsObj, T t) => (tcsObj as TaskCompletionSource<T>).SetResult(t));
        return tcs.Task;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        subscribers.UntrackAll();
    }

}

