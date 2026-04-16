namespace klooie.Gaming;
public sealed class PauseManager : IPausable
{
  
    private LeaseState<Recyclable>? pauseLease;
    private OpaqueLifetime? pauseLifetime;
    public Event<ILifetime> OnPaused { get; private set; } = Event<ILifetime>.Create();
    public ILifetime? PauseLifetime => pauseLifetime?.IsStillValid(pauseLifetime.Lease) == true ? pauseLifetime : null;

    public SynchronousScheduler Scheduler => Game.Current?.PausableScheduler;


    public bool PauseSoundWithPhysics { get; set; } = true;

    public bool IsPaused
    {
        get => Game.Current?.PausableScheduler?.IsPaused == true;
        set
        {
            if (Game.Current == null || Game.Current.IsDrainingOrDrained == true) return;
            if (value == IsPaused) return;

            if (value)
            {
                Game.Current.PausableScheduler.Pause();
                if(PauseSoundWithPhysics) Game.Current.Sound?.Pause();
                pauseLease?.UnTrackAndDispose();
                var backingLifetime = DefaultRecyclablePool.Instance.Rent();
                pauseLease = LeaseHelper.Track(backingLifetime);
                pauseLifetime = new OpaqueLifetime(backingLifetime);
                OnPaused.Fire(pauseLifetime);
                if (backingLifetime.IsStillValid(backingLifetime.Lease) == false)
                {
                    throw new InvalidOperationException($"Pause lifetime was invalidated during OnPaused notification. DisposalReason: {backingLifetime.DisposalReason ?? "<null>"}");
                }
            }
            else
            {
                Game.Current.PausableScheduler.Resume();
                Game.Current.Sound?.Resume(); // safe to call even if we didn't pause
                pauseLease?.UnTrackAndDispose();
                pauseLease = null;
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
        => e.Subscribe(new PauseState(pausable, handler), static s => s.Invoke(), subscriptionLifetime);

    public static void SubscribePaused<TScope>(this Event e, IPausable pausable, TScope scope, Action<TScope> handler, ILifetime subscriptionLifetime)
        => e.Subscribe(new PauseState<TScope>(pausable, scope, handler), static s => s.Invoke(), subscriptionLifetime);

    public static void SubscribePaused<TArg>(this Event<TArg> e, IPausable pausable, Action<TArg> handler, ILifetime subscriptionLifetime)
        => e.Subscribe(new PauseArgState<TArg>(pausable, handler), static (s, arg) => s.Invoke(arg), subscriptionLifetime);

    public static void SubscribePaused<TScope, TArg>(this Event<TArg> e, IPausable pausable, TScope scope, Action<TScope, TArg> handler, ILifetime subscriptionLifetime)
        => e.Subscribe(new PauseScopedArgState<TScope, TArg>(pausable, scope, handler), static (s, arg) => s.Invoke(arg), subscriptionLifetime);

    private readonly struct PauseState
    {
        private readonly IPausable pausable;
        private readonly Action handler;

        public PauseState(IPausable pausable, Action handler)
        {
            this.pausable = pausable;
            this.handler = handler;
        }

        public void Invoke()
        {
            if (pausable.IsPaused) return;
            handler();
        }
    }

    private readonly struct PauseState<TScope>
    {
        private readonly IPausable pausable;
        private readonly TScope scope;
        private readonly Action<TScope> handler;

        public PauseState(IPausable pausable, TScope scope, Action<TScope> handler)
        {
            this.pausable = pausable;
            this.scope = scope;
            this.handler = handler;
        }

        public void Invoke()
        {
            if (pausable.IsPaused) return;
            handler(scope);
        }
    }

    private readonly struct PauseArgState<TArg>
    {
        private readonly IPausable pausable;
        private readonly Action<TArg> handler;

        public PauseArgState(IPausable pausable, Action<TArg> handler)
        {
            this.pausable = pausable;
            this.handler = handler;
        }

        public void Invoke(TArg arg)
        {
            if (pausable.IsPaused) return;
            handler(arg);
        }
    }

    private readonly struct PauseScopedArgState<TScope, TArg>
    {
        private readonly IPausable pausable;
        private readonly TScope scope;
        private readonly Action<TScope, TArg> handler;

        public PauseScopedArgState(IPausable pausable, TScope scope, Action<TScope, TArg> handler)
        {
            this.pausable = pausable;
            this.scope = scope;
            this.handler = handler;
        }

        public void Invoke(TArg arg)
        {
            if (pausable.IsPaused) return;
            handler(scope, arg);
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
