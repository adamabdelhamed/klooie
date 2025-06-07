using System.Diagnostics;
namespace klooie;

public static class EventThrottleExtensions
{
    public static void SubscribeThrottled(this Event e, Action handler, ILifetime subscriptionLifetime, int maxHz)
        => EventThrottle.Create(e, handler, subscriptionLifetime, maxHz);
    
    public static void SubscribeThrottled(this Event e, object scope, Action<object> handler, ILifetime subscriptionLifetime, int maxCyclesPerSecond)
        => EventThrottle.Create(e, scope, handler, subscriptionLifetime, maxCyclesPerSecond);

    public static void SubscribeThrottled<T>(this Event<T> e, Action<T> handler, ILifetime subscriptionLifetime, int maxCyclesPerSecond)
        => EventThrottle<T>.Create(e, handler, subscriptionLifetime, maxCyclesPerSecond);

    public static void SubscribeThrottled<T>(this Event<T> e, object scope, Action<object, T> handler, ILifetime subscriptionLifetime, int maxCyclesPerSecond)
        => EventThrottle<T>.Create(e, scope, handler, subscriptionLifetime, maxCyclesPerSecond);

    private abstract class EventThrottleBase : Recyclable
    {
        protected double maxCyclesPerSecond;
        protected long minTicksBetweenCycles;
        protected long lastCycleTime;

        protected void Bind(int maxCyclesPerSecond)
        {
            this.maxCyclesPerSecond = Math.Clamp(maxCyclesPerSecond, 1, 100_000);
            minTicksBetweenCycles = (long)(Stopwatch.Frequency / this.maxCyclesPerSecond);
        }

        protected static void DisposeMe(object o) => ((Recyclable)o).TryDispose();

        protected bool CanExecute()
        {
            var now = Stopwatch.GetTimestamp();
            var prev = Volatile.Read(ref lastCycleTime);

            if (now < prev + minTicksBetweenCycles) return false;
            return Interlocked.CompareExchange(ref lastCycleTime, now, prev) == prev;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            lastCycleTime = 0;
            minTicksBetweenCycles = 0;
            maxCyclesPerSecond = 0;
        }
    }
    /// Throttles event invocations to at most N times per second. 
    /// Extra invocations within the window are ignored, not delayed.
    private sealed class EventThrottle : EventThrottleBase
    {
        private Action handler;
        private Action<object> scopedHandler;
        private object scope;
        public static EventThrottle Create(Event e, Action handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.handler = handler;
            e.Subscribe(inst, Throttle, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        public static EventThrottle Create(Event e, object scope, Action<object> handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.scopedHandler = handler;
            inst.scope = scope;
            e.Subscribe(inst, ThrottleScoped, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            handler = null;
            scopedHandler = null;
            scope = null;
        }

        private static void Throttle(object me)
        {
            var self = (EventThrottle)me;
            if (!self.CanExecute()) return;
            self.handler.Invoke();
        }

        private static void ThrottleScoped(object me)
        {
            var self = (EventThrottle)me;
            if (!self.CanExecute()) return;
            self.scopedHandler.Invoke(self.scope);
        }

        private sealed class Pool : RecycleablePool<EventThrottle>
        {
            public static Pool? _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override EventThrottle Factory() => new EventThrottle();
        }
    }

    private sealed class EventThrottle<T> : EventThrottleBase
    {
        private Action<T> handler;
        private Action<object, T> scopedHandler;
        private object scope;

        public static EventThrottle<T> Create(Event<T> e, Action<T> handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.handler = handler;
            e.Subscribe(inst, Throttle, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        public static EventThrottle<T> Create(Event<T> e, object scope, Action<object, T> handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.scopedHandler = handler;
            inst.scope = scope;
            e.Subscribe(inst, ThrottleScoped, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        private static void Throttle(object me, object arg)
        {
            var self = (EventThrottle<T>)me;
            if (!self.CanExecute()) return;
            self.handler.Invoke((T)arg);
        }

        private static void ThrottleScoped(object me, object arg)
        {
            var self = (EventThrottle<T>)me;
            if (!self.CanExecute()) return;
            self.scopedHandler!.Invoke(self.scope!, (T)arg);
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            scopedHandler = null;
            handler = null;
            scope = null;
        }

        private sealed class Pool : RecycleablePool<EventThrottle<T>>
        {
            public static Pool? _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override EventThrottle<T> Factory() => new EventThrottle<T>();
        }
    }
}