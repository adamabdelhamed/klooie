namespace PowerArgs;
    /// <summary>
    /// A lifetime aware event
    /// </summary>
    public class Event
    {
        private List<(Action, ILifetimeManager)> subscribers;
        private List<(Action<object>, object, ILifetimeManager)> subscribersWithParams;

        /// <summary>
        /// returns true if there is at least one subscriber
        /// </summary>
        public bool HasSubscriptions => subscribers.Any() || subscribersWithParams.Any();
         
        public Event()
        {
            subscribers = new List<(Action, ILifetimeManager)>();
            subscribersWithParams = new List<(Action<object>, object, ILifetimeManager)>();
        }

        /// <summary>
        /// Fires the event. All subscribers will be notified
        /// </summary>
        public void Fire()
        {
            foreach(var subscriber in subscribers.ToArray())
            {
                subscriber.Item1?.Invoke();
            }

            foreach (var subscriber in subscribersWithParams.ToArray())
            {
                subscriber.Item1?.Invoke(subscriber.Item2);
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
            var subRecord = (handler, sub);
            subscribers.Add(subRecord);
            sub.OnDisposed(()=> subscribers.Remove(subRecord));
            return sub;
        }

        public ILifetime SubscribeUnmanaged(Action<object> handler, object param)
        {
            var sub = new Lifetime();
            var subRecord = (handler, param, sub);
            subscribersWithParams.Add(subRecord);
            sub.OnDisposed(() => subscribersWithParams.Remove(subRecord));
            return sub;
        }

      

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

        public Task CreateNextFireTask()
        {
            var tcs = new TaskCompletionSource();
            this.SubscribeOnce(()=> tcs.SetResult());
            return tcs.Task;
        }
    }

public class Event<T>
{
    private Event innerEvent = new Event();
    private T args;

    public void SubscribeForLifetime(Action<T> handler, ILifetimeManager lt) =>
        innerEvent.SubscribeForLifetime(() => handler(args), lt);

    public void SubscribeOnce(Action<T> handler) =>
        innerEvent.SubscribeOnce(() => handler(args));

    public ILifetime SubscribeUnmanaged(Action<T> handler) =>
        innerEvent.SubscribeUnmanaged(() => handler(args));

    public void Fire(T args)
    {
        this.args = args;
        innerEvent.Fire();
        this.args = default;
    }

    /// <summary>
    /// Creates a lifetime that will end the next time this
    /// event fires
    /// </summary>
    /// <returns>a lifetime that will end the next time this event fires</returns>
    public Lifetime CreateNextFireLifetime() => innerEvent.CreateNextFireLifetime();

    public Task<T> CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource<T>();
        this.SubscribeOnce(args => tcs.SetResult(args));
        return tcs.Task;
    }
}