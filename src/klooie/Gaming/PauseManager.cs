namespace klooie.Gaming;
public sealed class PauseManager : IPausable
{
    private Recyclable? pauseLifetime;
    public Event<ILifetime> OnPaused { get; private set; } = Event<ILifetime>.Create();
    public Recyclable? PauseLifetime => pauseLifetime;

    public SynchronousScheduler Scheduler => Game.Current.PausableScheduler;


    public bool PauseSoundWithPhysics { get; set; } = true;

    public bool IsPaused
    {
        get => Game.Current.PausableScheduler.IsPaused;
        set
        {
            if (value == IsPaused) return;

            if (value)
            {
                Game.Current.PausableScheduler.Pause();
                if(PauseSoundWithPhysics) Game.Current.Sound?.Pause();
                pauseLifetime = DefaultRecyclablePool.Instance.Rent();
                OnPaused.Fire(pauseLifetime);
            }
            else
            {
                Game.Current.PausableScheduler.Resume();
                Game.Current.Sound?.Resume(); // safe to call even if we didn't pause
                pauseLifetime?.Dispose();
                pauseLifetime = null;
            }
        }
    }
}

/// <summary>
/// Provides pause-aware event subscription extension methods.
/// </summary>
public static class EventPauseExtensions
{
    public static void SubscribePaused(this Event e, IPausable pausable, Action handler, ILifetime subscriptionLifetime)
        => PauseHandler.Create(e, pausable, handler, subscriptionLifetime);

    public static void SubscribePaused<TScope>(this Event e, IPausable pausable, TScope scope, Action<TScope> handler, ILifetime subscriptionLifetime)
        => PauseHandler<TScope>.Create(e, pausable, scope, handler, subscriptionLifetime);

    public static void SubscribePaused<TArg>(this Event<TArg> e, IPausable pausable, Action<TArg> handler, ILifetime subscriptionLifetime)
        => PauseArgHandler<TArg>.Create(e, pausable, handler, subscriptionLifetime);

    public static void SubscribePaused<TScope, TArg>(this Event<TArg> e, IPausable pausable, TScope scope, Action<TScope, TArg> handler, ILifetime subscriptionLifetime)
        => PauseScopedArgHandler<TScope, TArg>.Create(e, pausable, scope, handler, subscriptionLifetime);

    private sealed class PauseHandler : Recyclable
    {
        private IPausable pausable;
        private Action handler;

        public static PauseHandler Create(Event e, IPausable pausable, Action handler, ILifetime lifetime)
        {
            var inst = Pool.Instance.Rent();
            inst.pausable = pausable;
            inst.handler = handler;
            e.Subscribe(inst, static me => me.InvokeIfNotPaused(), lifetime);
            lifetime.OnDisposed(inst, TryDisposeMe);
            return inst;
        }

        private void InvokeIfNotPaused()
        {
            if (pausable.IsPaused) return;
            handler?.Invoke();
        }

        protected override void OnReturn()
        {
            pausable = null;
            handler = null;
            base.OnReturn();
        }

        private sealed class Pool : RecycleablePool<PauseHandler>
        {
            public static Pool _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override PauseHandler Factory() => new PauseHandler();
        }
    }

    private sealed class PauseHandler<TScope> : Recyclable
    {
        private IPausable pausable;
        private TScope scope;
        private Action<TScope> handler;

        public static PauseHandler<TScope> Create(Event e, IPausable pausable, TScope scope, Action<TScope> handler, ILifetime lifetime)
        {
            var inst = Pool.Instance.Rent();
            inst.pausable = pausable;
            inst.scope = scope;
            inst.handler = handler;
            e.Subscribe(inst, static me => me.InvokeIfNotPaused(), lifetime);
            lifetime.OnDisposed(inst, TryDisposeMe);
            return inst;
        }

        private void InvokeIfNotPaused()
        {
            if (pausable.IsPaused) return;
            handler?.Invoke(scope);
        }

        protected override void OnReturn()
        {
            pausable = null;
            handler = null;
            scope = default;
            base.OnReturn();
        }

        private sealed class Pool : RecycleablePool<PauseHandler<TScope>>
        {
            public static Pool _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override PauseHandler<TScope> Factory() => new PauseHandler<TScope>();
        }
    }

    private sealed class PauseArgHandler<TArg> : Recyclable
    {
        private IPausable pausable;
        private Action<TArg> handler;

        public static PauseArgHandler<TArg> Create(Event<TArg> e, IPausable pausable, Action<TArg> handler, ILifetime lifetime)
        {
            var inst = Pool.Instance.Rent();
            inst.pausable = pausable;
            inst.handler = handler;
            e.Subscribe<PauseArgHandler<TArg>>(inst, static (me, arg) => me.InvokeIfNotPaused(arg), lifetime);
            lifetime.OnDisposed(inst, TryDisposeMe);
            return inst;
        }

        private void InvokeIfNotPaused(TArg arg)
        {
            if (pausable.IsPaused) return;
            handler?.Invoke(arg);
        }

        protected override void OnReturn()
        {
            pausable = null;
            handler = null;
            base.OnReturn();
        }

        private sealed class Pool : RecycleablePool<PauseArgHandler<TArg>>
        {
            public static Pool _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override PauseArgHandler<TArg> Factory() => new PauseArgHandler<TArg>();
        }
    }

    private sealed class PauseScopedArgHandler<TScope, TArg> : Recyclable
    {
        private IPausable pausable;
        private TScope scope;
        private Action<TScope, TArg> handler;

        public static PauseScopedArgHandler<TScope, TArg> Create(Event<TArg> e, IPausable pausable, TScope scope, Action<TScope, TArg> handler, ILifetime lifetime)
        {
            var inst = Pool.Instance.Rent();
            inst.pausable = pausable;
            inst.scope = scope;
            inst.handler = handler;
            e.Subscribe<PauseScopedArgHandler<TScope, TArg>>(inst, static (me, arg) => me.InvokeIfNotPaused(arg), lifetime);
            lifetime.OnDisposed(inst, TryDisposeMe);
            return inst;
        }

        private void InvokeIfNotPaused(TArg arg)
        {
            if (pausable.IsPaused) return;
            handler?.Invoke(scope, arg);
        }

        protected override void OnReturn()
        {
            pausable = null;
            handler = null;
            scope = default;
            base.OnReturn();
        }

        private sealed class Pool : RecycleablePool<PauseScopedArgHandler<TScope, TArg>>
        {
            public static Pool _instance;
            public static Pool Instance => _instance ??= new Pool();
            public override PauseScopedArgHandler<TScope, TArg> Factory() => new PauseScopedArgHandler<TScope, TArg>();
        }
    }
}

/// <summary>
/// Provides pause state.
/// </summary>
public interface IPausable
{
    bool IsPaused { get; }
}
