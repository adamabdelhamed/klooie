namespace PowerArgs;
/// <summary>
/// A lifetime aware event
/// </summary>
public class Event
{
    private List<Subscription> subscribers;
    private List<SubscriptionWithParam> subscribersWithParams;

    /// <summary>
    /// returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => subscribers.Any() || subscribersWithParams.Any();
         
    /// <summary>
    /// creates a new event
    /// </summary>
    public Event()
    {
        subscribers = new List<Subscription>();
        subscribersWithParams = new List<SubscriptionWithParam>();
    }

    /// <summary>
    /// Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire()
    {
        var subscriberCount = subscribers.Count;
        var subscriberCountWithParams = subscribersWithParams.Count;
        var buffer = NotificationBufferPool.Instance.Get(subscriberCount, subscriberCountWithParams);
        try
        {
            var subSpan = buffer.MainBuffer.AsSpan();
            var paramSpan = buffer.ParamBuffer.AsSpan();

            for (var i = 0; i < subscriberCount; i++)
            {
                subSpan[i] = subscribers[i];
            }

            for (var i = 0; i < subscriberCountWithParams; i++)
            {
                paramSpan[i] = subscribersWithParams[i];
            }

            for (var i = 0; i < subscriberCount; i++)
            {
                if (subSpan[i].Lifetime.ShouldContinue)
                {
                    subSpan[i].Callback();
                }
            }

            for (var i = 0; i < subscriberCountWithParams; i++)
            {
                if (paramSpan[i].Lifetime.ShouldContinue)
                {
                    paramSpan[i].Callback(paramSpan[i].Param);
                }
            }
        }
        finally
        {
            NotificationBufferPool.Instance.Return(buffer);
        }
    }

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires 
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <returns>A subscription that can be disposed when you no loner want to be notified from this event</returns>
    public ILifetime SubscribeUnmanaged(Action handler)
    {
        var sub = new Lifetime();
        var subRecord = new Subscription() { Callback = handler, Lifetime = sub.Manager };
        subscribers.Add(subRecord);
        sub.OnDisposed(()=> subscribers.Remove(subRecord));
        return sub;
    }

    /// <summary>
    /// Subscribes to an event and returns a lifetime that you
    /// can dispose manually when you no longer want to be notified
    /// </summary>
    /// <param name="handler">the notification callback</param>
    /// <param name="param">the parameter to send to the callback</param>
    /// <returns>a lifetime that you can dispose manually when you no longer want to be notified</returns>
    public ILifetime SubscribeUnmanaged(Action<object> handler, object param)
    {
        var sub = new Lifetime();
        var subRecord = new SubscriptionWithParam() { Callback = handler, Lifetime = sub.Manager, Param = param };
        subscribersWithParams.Add(subRecord);
        sub.OnDisposed(() => subscribersWithParams.Remove(subRecord));
        return sub;
    }

    /// <summary>
    /// calls the callback now and subscribes to the event
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <returns>a lifetime that you can dispose manually when you no longer want to be notified</returns>
    public ILifetime SynchronizeUnmanaged(Action handler)
    {
        handler();
        return SynchronizeUnmanaged(handler);
    }

    /// <summary>
    /// Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    /// when the lifetime associated with the given lifetime manager is disposed.
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    public void SubscribeForLifetime(Action handler, ILifetimeManager lifetimeManager)
    {
        var lt = SubscribeUnmanaged(handler);
        lifetimeManager.OnDisposed(lt);
    }

    /// <summary>
    /// calls the callback now and subscribes to the event
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lifetimeManager">the lifetime of the subscription</param>
    public void SynchronizeForLifetime(Action handler, ILifetimeManager lifetimeManager)
    {
        handler();
        SubscribeForLifetime(handler, lifetimeManager);
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

        SubscribeForLifetime(wrappedAction, lt);
    }

    /// <summary>
    /// Subscribes once
    /// </summary>
    /// <param name="handler">the callback to call</param>
    /// <param name="param">the object to pass to the callback</param>
    public void SubscribeOnce(Action<object> handler, object param)
    {
        Action wrappedAction = null;
        var lt = new Lifetime();
        wrappedAction = () =>
        {
            try
            {
                handler(param);
            }
            finally
            {
                lt.Dispose();
            }
        };

        SubscribeForLifetime(wrappedAction, lt);
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

    private class NotificationBuffer
    {
        public Subscription[] MainBuffer;
        public SubscriptionWithParam[] ParamBuffer;
    }

    private class Subscription
    {
        public Action Callback { get; set; }
        public ILifetimeManager Lifetime { get; set; }
    }

    private class SubscriptionWithParam
    {
        public Action<object> Callback { get; set; }
        public ILifetimeManager Lifetime { get; set; }
        public object Param { get; set; }
    }

    private class NotificationBufferPool
    {
#if DEBUG
        internal static int HitCount = 0;
        internal static int MissCount = 0;
        internal static int PartialHitCount = 0;
#endif
        private List<NotificationBuffer> pool = new List<NotificationBuffer>();
        public static readonly NotificationBufferPool Instance = new NotificationBufferPool();

        public NotificationBuffer Get(int mainSize, int paramSize)
        {
            lock (pool)
            {
                mainSize = mainSize < 100 ? 100 : mainSize;
                paramSize = paramSize < 100 ? 100 : paramSize;
                // if no buffers, create one
                if (pool.Count == 0)
                {
#if DEBUG
                MissCount++;
#endif
                    return new NotificationBuffer()
                    {
                        MainBuffer = new Subscription[mainSize * 2],
                        ParamBuffer = new SubscriptionWithParam[paramSize * 2]
                    };
                }

                // try to find an existing buffer that is big enough
                for (var i = 0; i < pool.Count; i++)
                {
                    if (pool[i].MainBuffer.Length >= mainSize && pool[i].ParamBuffer.Length >= paramSize)
                    {
                        var toRent = pool[i];
                        pool.RemoveAt(i);
#if DEBUG
                    HitCount++;
#endif
                        return toRent;
                    }
                }

#if DEBUG
            PartialHitCount++;
#endif

                // resize existing buffer to be big enough and use it
                var firstBuffer = pool.First();
                firstBuffer.MainBuffer = new Subscription[mainSize * 2];
                firstBuffer.ParamBuffer = new SubscriptionWithParam[paramSize * 2];
                pool.RemoveAt(0);
                return firstBuffer;
            }
        }

        public void Return(NotificationBuffer buffer)
        {
            lock (pool)
            {
                pool.Add(buffer);
            }
        }
    }
}

/// <summary>
/// An event that has an argument of the given type
/// </summary>
/// <typeparam name="T">the argument type</typeparam>
public class Event<T>
{
    private Event innerEvent = new Event();
    private Stack<T> args = new Stack<T>();

    /// <summary>
    /// Subscribes for the given lifetime
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <param name="lt">the lifetime</param>
    public void SubscribeForLifetime(Action<T> handler, ILifetimeManager lt) =>
        innerEvent.SubscribeForLifetime(() => handler(args.Peek()), lt);

    /// <summary>
    /// Subscribes for one notification
    /// </summary>
    /// <param name="handler">the callback</param>
    public void SubscribeOnce(Action<T> handler) =>
        innerEvent.SubscribeOnce(() => handler(args.Peek()));

    /// <summary>
    /// Subscribes to the event
    /// </summary>
    /// <param name="handler">the callback</param>
    /// <returns>a lifetime that you can dispose if you no longer want to be notified</returns>
    public ILifetime SubscribeUnmanaged(Action<T> handler) =>
        innerEvent.SubscribeUnmanaged(() => handler(args.Peek()));

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
}

/*
 
[MemoryDiagnoser]
public class EventBenchmark
{
    public int Count => count;

    int numberOfEvents = 1;
    int numberOfSubscribersPerEvent = 100;
    int numberOfFires = 50;
    int count = 0;
    private Event[] events;

    [GlobalSetup]
    public void Setup()
    {
        events = new Event[numberOfEvents];
        for(var i = 0; i < numberOfEvents; i++)
        {
            events[i] = new Event();
            for (var j = 0; j < numberOfSubscribersPerEvent; j++)
            {
                events[i].SubscribeForLifetime(() => count++, Lifetime.Forever);
            }
        }
    }

    [Benchmark]
    public void BenchmarkFire()
    {
        for(var i = 0; i < events.Length; i++)
        {
            for (var j = 0; j < numberOfFires; j++)
            {
                events[i].Fire();
            }
        }
    }
}

 
 */ 