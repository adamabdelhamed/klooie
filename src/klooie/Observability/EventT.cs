namespace klooie;
public interface IEventT
{
    public Stack<object> args { get; }
}

/// <summary>
/// An event that has an argument of the given type
/// </summary>
/// <typeparam name="T">the argument type</typeparam>
public sealed class Event<T> : Recyclable, IEventT
{
    private Event? innerEvent;
    public Stack<object> args { get; } = new Stack<object>(); // because notifications can be re-entrant

    public bool HasSubscriptions => innerEvent?.HasSubscriptions == true;

    public int SubscriberCount => innerEvent?.SubscriberCount ?? 0;

    private Event() { }

    private static readonly LazyPool<Event<T>> Pool = new LazyPool<Event<T>>(static () => new Event<T>());
    public static Event<T> Create() => Pool.Value.Rent();
    public static Event<T> Create(out int lease) => Pool.Value.Rent(out lease);

    /// <summary>
    /// Subscribes for the given lifetime.
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lt">the lifetime</param>
    public void Subscribe(Action<T> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? Event.Create();
        innerEvent.Subscribe(() => Execute(handler), lt);
    }

    // -----------------------------------------------
    /// <summary>
    /// Subscribes for the given lifetime, passing both a scope and the event argument.
    /// This avoids capturing local variables if you use a static method.
    /// </summary>
    public void Subscribe<TScope>(TScope scope, Action<object, object> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? Event.Create();
        innerEvent.eventSubscribers ??= SubscriptionListPool.Rent();
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Bind(lt);
        subscription.ScopedCallback = StaticCallback;
        subscription.TScopedCallback = handler;
        subscription.Scope = subscription;
        subscription.TScope = scope;
        subscription.eventT = this;
        subscription.Subscribers = innerEvent.eventSubscribers;
        innerEvent.eventSubscribers.Add(subscription);
        lt.OnDisposed(subscription, TryDisposeMe);

    }

    private static void TryDisposeMe(object me) => ((Recyclable)me).TryDispose();

    private static void StaticCallback(object me)
    {
        var sub = (Subscription)me;
        if (sub.eventT.args.TryPeek(out var arg) == false)
        {
            throw new InvalidOperationException("Event<T> is firing without args in the stack");
        }
        sub.TScopedCallback.Invoke(sub.TScope, arg);
    }

    private void Execute(Action<T> handler)
    {
        if (args.TryPeek(out var arg) == false)
        {
            throw new InvalidOperationException("Event<T> is firing without args present.");
        }

        handler((T)arg);
    }

    private void Execute<TScope>(TScope scope, Action<TScope, T> handler)
    {
        if (args.TryPeek(out var arg) == false)
        {
            throw new InvalidOperationException("Event<T> is firing without args present.");
        }

        handler(scope, (T)arg);
    }


    /// <summary>
    /// Subscribes with priority for the given lifetime.
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lt">the lifetime</param>
    public void SubscribeWithPriority(Action<T> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? Event.Create();
        innerEvent.SubscribeWithPriority(() => Execute(handler), lt);
    }

    // -----------------------------------------------
    //  NEW: Overload that accepts a scope object
    // -----------------------------------------------
    /// <summary>
    /// Subscribes with priority for the given lifetime, passing both a scope and the event argument.
    /// </summary>
    public void SubscribeWithPriority<TScope>(TScope scope, Action<TScope, T> handler, ILifetime lt)
    {
        innerEvent = innerEvent ?? Event.Create();
        innerEvent.SubscribeWithPriority(scope, s => Execute((TScope)s!, handler), lt);
    }

    /// <summary>
    /// Subscribes once for the given callback.
    /// </summary>
    /// <param name="handler">the callback</param>
    public void SubscribeOnce(Action<T> handler)
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        bool fired = false;
        Action wrappedHandler = null;
        wrappedHandler = () =>
        {
            if (fired) return;
            fired = true;
            try { handler((T)args.Peek()); }
            finally { lt.Dispose(); }
        };
        innerEvent = innerEvent ?? Event.Create();
        innerEvent.Subscribe(wrappedHandler, lt);
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
        bool fired = false;
        Action wrappedAction = null;
        wrappedAction = () =>
        {
            if (fired) return;
            fired = true;
            try
            {
                Execute(scope, handler);
            }
            finally
            {
                lt.Dispose();
            }
        };

        innerEvent = innerEvent ?? Event.Create();
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
    public void Fire(T args, DelayState dependencies)
    {
        if (innerEvent == null) return;

        this.args.Push(args);
        try
        {
            innerEvent.Fire(dependencies);
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
        innerEvent = innerEvent ?? Event.Create();
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

