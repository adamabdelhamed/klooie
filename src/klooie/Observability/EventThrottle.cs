using System.Diagnostics;
namespace klooie;

public static class EventThrottleExtensions
{
    public static void SubscribeThrottled(this Event e, Action handler, ILifetime subscriptionLifetime, int maxHz)
        => EventThrottle.Create(e, handler, subscriptionLifetime, maxHz);
    
    public static void SubscribeThrottled<TScope>(this Event e, TScope scope, Action<TScope> handler, ILifetime subscriptionLifetime, int maxCyclesPerSecond)
        => EventThrottle<TScope>.Create(e, scope, handler, subscriptionLifetime, maxCyclesPerSecond);

    public static void SubscribeThrottled<TArg>(this Event<TArg> e, Action<TArg> handler, ILifetime subscriptionLifetime, int maxCyclesPerSecond)
        => ArgEventThrottle<TArg>.Create(e, handler, subscriptionLifetime, maxCyclesPerSecond);

    public static void SubscribeThrottled<TScope,TArg>(this Event<TArg> e, TScope scope, Action<TScope, TArg> handler, ILifetime subscriptionLifetime, int maxCyclesPerSecond)
        => ScopedAndArgEventThrottle<TScope,TArg>.Create(e, scope, handler, subscriptionLifetime, maxCyclesPerSecond);

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

    private sealed class ArgEventThrottle<TArg> : EventThrottleBase
    {
        private Action<TArg> handler;

        public static ArgEventThrottle<TArg> Create(Event<TArg> e, Action<TArg> handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.handler = handler;
            e.Subscribe<ArgEventThrottle<TArg>>(inst, Throttle, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        private static void Throttle(ArgEventThrottle<TArg> me, TArg arg)
        {
            if (!me.CanExecute()) return;
            me.handler.Invoke(arg);
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            handler = null;
        }

        private sealed class Pool : RecycleablePool<ArgEventThrottle<TArg>>
        {
            public static Pool? _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override ArgEventThrottle<TArg> Factory() => new ArgEventThrottle<TArg>();
        }
    }

    private sealed class EventThrottle<TScope> : EventThrottleBase
    {
        private Action<TScope> scopedHandler;
        private TScope scope;

        public static EventThrottle<TScope> Create(Event e, TScope scope, Action<TScope> handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.scopedHandler = handler;
            inst.scope = scope;
            e.Subscribe<EventThrottle<TScope>>(inst, ThrottleScoped, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        private static void ThrottleScoped(EventThrottle<TScope> me)
        {
            if (!me.CanExecute()) return;
            me.scopedHandler!.Invoke(me.scope);
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            scopedHandler = null;
            scope = default;
        }

        private sealed class Pool : RecycleablePool<EventThrottle<TScope>>
        {
            public static Pool? _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override EventThrottle<TScope> Factory() => new EventThrottle<TScope>();
        }
    }

    private sealed class ScopedAndArgEventThrottle<TScope, TArg> : EventThrottleBase
    {
        private Action<TScope, TArg> scopedHandler;
        private TScope scope;

        public static ScopedAndArgEventThrottle<TScope, TArg> Create(Event<TArg> e, TScope scope, Action<TScope, TArg> handler, ILifetime lifetime, int maxCyclesPerSecond)
        {
            var inst = Pool.Instance.Rent();
            inst.Bind(maxCyclesPerSecond);
            inst.scopedHandler = handler;
            inst.scope = scope;
            e.Subscribe<ScopedAndArgEventThrottle<TScope, TArg>>(inst, ThrottleScoped, lifetime);
            lifetime.OnDisposed(inst, DisposeMe);
            return inst;
        }

        private static void ThrottleScoped(ScopedAndArgEventThrottle<TScope, TArg> me, TArg arg)
        {
            if (!me.CanExecute()) return;
            me.scopedHandler!.Invoke(me.scope, (TArg)arg);
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            scopedHandler = null;
            scope = default;
        }

        private sealed class Pool : RecycleablePool<ScopedAndArgEventThrottle<TScope,TArg>>
        {
            public static Pool? _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override ScopedAndArgEventThrottle<TScope,TArg> Factory() => new ScopedAndArgEventThrottle<TScope, TArg>();
        }
    }
}