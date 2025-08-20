using System.Diagnostics;
using klooie.Gaming;
namespace klooie;

public sealed class SyncronousScheduler
{
    public enum ExecutionMode
    {
        AfterPaint,
        EndOfCycle
    }

    private ConsoleApp parent;
    private LeaseState<SchedulerLoopLifetime> schedulerLoopLease;
    private long? pausedTime;
    private List<DelayState>? delayStates;
    public bool IsPaused => pausedTime.HasValue;

    public ExecutionMode Mode { get; set; } = SyncronousScheduler.ExecutionMode.AfterPaint;

    // NEW: opt-in collider time dilation per scheduler
    public bool UseColliderTimeDilation { get; set; } = false;

    // NEW: last loop wall-clock timestamp to integrate scaled time
    private long lastProcessTimestamp;

    // NEW: helper for current speed ratio (null-safe)
    private double CurrentSpeedRatio => UseColliderTimeDilation
        ? (Game.Current?.MainColliderGroup?.SpeedRatio ?? 1.0)
        : 1.0;

    public SyncronousScheduler(ConsoleApp parent)
    {
        this.parent = parent;
        parent.OnDisposed(Cleanup);
    }

    private void Cleanup()
    {
        schedulerLoopLease?.UnTrackAndDispose();
        schedulerLoopLease = null;
    }

    internal void Pause()
    {
        pausedTime = Stopwatch.GetTimestamp();
    }

    internal void Resume()
    {
        if (pausedTime == null) return;

        // We simply reset the loop's integration point so no time "jumps" on resume.
        lastProcessTimestamp = Stopwatch.GetTimestamp();

        pausedTime = null;
    }

    public Task Delay(double delayMs)
    {
        var tcs = new TaskCompletionSource();
        Delay(delayMs, tcs, static tcs => tcs.SetResult());
        return tcs.Task;
    }

    public void Delay<TScope>(double delayMs, TScope state, Action<TScope> callback) => EnsureDelayLoopIsRunning(StatefulWorkItem<TScope>.Create(state, callback, delayMs));
    public void Delay(double delayMs, Action callback) => EnsureDelayLoopIsRunning(StatelessWorkItem.Create(callback, delayMs));

    public void DelayIfValid<TState>(double delayMs, TState statefulScope, Action<TState> callback) where TState : DelayState
    {
        if (delayStates == null)
        {
            delayStates = new List<DelayState>();
            ConsoleApp.Current.OnDisposed(this, DisposeStates);
        }
        delayStates.Add(statefulScope);
        var delayStateInstance = DelayIfValidInstance<TState>.Create(callback, statefulScope);
        Delay(delayMs, delayStateInstance, InvokeDelayCallbackIfAllDependenciesAreValid);
    }

    public void DelayThenDisposeAllDependencies(double delayMs, DelayState statefulScope)
    {
        if (delayStates == null)
        {
            delayStates = new List<DelayState>();
            ConsoleApp.Current.OnDisposed(this, DisposeStates);
        }
        // TODO: This is an ever growing list during the lifetime of the app. This could cause two issues.
        // First, the dispose loop at the end of the app could be slow due to the number of states being very large.
        // Second, and more importantly, these states are not always disposed by this class' code. Sometimes (frequently)
        // the caller disposes of it. At the very least, this code needs to track the lease of the state and pass it to
        // TryDispose during DisposeStates so that we don't accidentally dispose of a state that has been recycled.
        // It's not super critical since DisposeStates is only called at the end of the app, but we should defend against
        // it in case there are any unforseeable scenarios where DelayState objects are used across multiple app lifetimes.
        delayStates.Add(statefulScope);
        Delay(delayMs, statefulScope, DisposeAllDependneciesFromDelayState);
    }

    private static void InvokeDelayCallbackIfAllDependenciesAreValid<TState>(DelayIfValidInstance<TState> delayIfValidInstance) where TState : DelayState
    {
        try
        {
            if (delayIfValidInstance.Lease.IsRecyclableValid == false)
            {
                return;
            }

            if (delayIfValidInstance.DelayState.AreAllDependenciesValid == false)
            {
                delayIfValidInstance.DelayState.TryDispose();
                return;
            }
            delayIfValidInstance.Callback.Invoke(delayIfValidInstance.DelayState);
        }
        finally
        {
            delayIfValidInstance.Dispose();
        }
    }

    private static void DisposeAllDependneciesFromDelayState(DelayState state)
    {
        state.DisposeAllValidDependencies();
        state.TryDispose();
    }

    private static void DisposeStates(object innerLoopObs)
    {
        var _this = (SyncronousScheduler)innerLoopObs;
        if (_this.delayStates == null) return;
        foreach (var ds in _this.delayStates)
        {
            ds.TryDispose();
        }
        _this.delayStates.Clear();
        _this.delayStates = null;
    }

    private void EnsureDelayLoopIsRunning(ScheduledWorkItem loopState)
    {
        if (schedulerLoopLease == null || !schedulerLoopLease.IsRecyclableValid) InitDelayLoop();
        schedulerLoopLease.Recyclable!.PendingWorkItems.Items.Add(loopState);
    }

    public void InitDelayLoop()
    {
        var loopLifetime = SchedulerLoopLifetime.Create();
        schedulerLoopLease = LeaseHelper.Track(loopLifetime);

        // Initialize integration anchor for scaled time
        lastProcessTimestamp = Stopwatch.GetTimestamp();

        if (Mode == ExecutionMode.AfterPaint)
        {
            parent.AfterPaint.Subscribe(loopLifetime, Process, loopLifetime);
        }
        else if (Mode == ExecutionMode.EndOfCycle)
        {
            parent.EndOfCycle.Subscribe(loopLifetime, Process, loopLifetime);
        }
        else
        {
            throw new ArgumentException("Invalid ExecutionMode specified for SyncronousScheduler", nameof(Mode));
        }
    }

    private void Process(SchedulerLoopLifetime loopLifetime)
    {
        if (pausedTime.HasValue) return;

        var now = Stopwatch.GetTimestamp();
        var wallDeltaTicks = now - lastProcessTimestamp;
        if (wallDeltaTicks < 0) wallDeltaTicks = 0;

        var ratio = CurrentSpeedRatio;
        if (!double.IsFinite(ratio) || ratio < 0) ratio = 1.0;
        var scaledDeltaTicks = wallDeltaTicks * ratio;

        var pending = loopLifetime.PendingWorkItems;

        // Re-entrancy guard: only process items that existed at entry.
        int end = pending.Count;
        int i = 0;
        while (i < end)
        {
            var item = pending.Items[i];

            // Integrate this frame's scaled time for the item we're touching.
            item.AccumulatedScaledElapsedTicks += scaledDeltaTicks;

            // Use your existing due check; if you added IsDue, use that instead.
            if (item.TimeUntilDue > TimeSpan.Zero)
            {
                i++; // keep moving forward within the frozen window
                continue;
            }

            FrameDebugger.RegisterTask("ScheduledWork");
            item.InvokeCallback();
            item.Dispose();

            // Remove without advancing i; the next original item slides into i.
            pending.Items.RemoveAt(i);
            end--; // shrink the frozen window so we still terminate
        }

        lastProcessTimestamp = now;

        // Dispose AFTER the iteration, and only if nothing remains.
        if (pending.Count == 0)
        {
            loopLifetime.Dispose();
        }
    }

    internal class DelayIfValidInstance<TState> : Recyclable where TState : DelayState
    {
        public Action<TState> Callback { get; set; }
        public TState DelayState { get; set; }

        public LeaseState<TState> Lease { get; private set; }

        internal static LazyPool<DelayIfValidInstance<TState>> pool = new LazyPool<DelayIfValidInstance<TState>>(() => new DelayIfValidInstance<TState>());
        private DelayIfValidInstance() { }
        public static DelayIfValidInstance<TState> Create(Action<TState> callback, TState state)
        {
            var instance = pool.Value.Rent();
            instance.Callback = callback;
            instance.DelayState = state;
            instance.Lease = LeaseHelper.Track(state);
            return instance;
        }
        protected override void OnReturn()
        {
            base.OnReturn();
            Lease.Dispose();
            Callback = null;
            DelayState = null;
        }
    }

    internal abstract class ScheduledWorkItem : Recyclable
    {
        // CHANGED: store delay target in "scaled ticks" space and accumulate progression
        public double DelayScaledTicks { get; protected set; }
        internal double AccumulatedScaledElapsedTicks;

        internal double RemainingScaledTicks => DelayScaledTicks - AccumulatedScaledElapsedTicks;
        public bool IsDue => RemainingScaledTicks <= 0;

        // keep a TimeUntilDue for diagnostics, but compute using Stopwatch.Frequency (seconds -> ticks)
        public TimeSpan TimeUntilDue =>
            RemainingScaledTicks <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(RemainingScaledTicks / Stopwatch.Frequency);
        public abstract void InvokeCallback();
        public override string ToString()
        {
            var ms = RemainingScaledTicks / Stopwatch.Frequency * 1000.0;
            return Math.Round(ms) + "ms until due";
        }
    }

    internal class StatelessWorkItem : ScheduledWorkItem
    {
        private Action Callback;
        internal static LazyPool<StatelessWorkItem> pool = new LazyPool<StatelessWorkItem>(() => new StatelessWorkItem());
        private StatelessWorkItem() { }
        public static StatelessWorkItem Create(Action callback, double delay)
        {
            var instance = pool.Value.Rent();
            instance.Callback = callback ?? throw new ArgumentNullException(nameof(callback), "Callback cannot be null");
            instance.DelayScaledTicks = delay * Stopwatch.Frequency / 1000.0;
            instance.AccumulatedScaledElapsedTicks = 0;
            return instance;
        }
        public override void InvokeCallback() => Callback?.Invoke();
        protected override void OnReturn()
        {
            base.OnReturn();
            Callback = null;
        }
    }

    internal class StatefulWorkItem<TScope> : ScheduledWorkItem
    {
        private TScope State;
        private Action<TScope>? Callback;
        public override void InvokeCallback() => Callback?.Invoke(State);
        private StatefulWorkItem() { }

        internal static LazyPool<StatefulWorkItem<TScope>> pool = new LazyPool<StatefulWorkItem<TScope>>(() => new StatefulWorkItem<TScope>());
        public static StatefulWorkItem<TScope> Create(TScope state, Action<TScope> callback, double delay)
        {
            var instance = pool.Value.Rent();
            instance.State = state ?? throw new ArgumentNullException(nameof(state), "State cannot be null");
            instance.Callback = callback ?? throw new ArgumentNullException(nameof(callback), "Callback cannot be null");
            instance.DelayScaledTicks = delay * Stopwatch.Frequency / 1000.0;
            instance.AccumulatedScaledElapsedTicks = 0;
            return instance;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Callback = null;
            State = default;
        }
    }

    private class SchedulerLoopLifetime : Recyclable
    {
        public RecyclableList<ScheduledWorkItem> PendingWorkItems { get; private set; }
        private static LazyPool<SchedulerLoopLifetime> pool = new LazyPool<SchedulerLoopLifetime>(() => new SchedulerLoopLifetime());
        private SchedulerLoopLifetime() { }
        public static SchedulerLoopLifetime Create()
        {
            var instance = pool.Value.Rent();
            instance.PendingWorkItems = RecyclableListPool<ScheduledWorkItem>.Instance.Rent();
            return instance;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            for (int i = 0; i < PendingWorkItems.Count; i++)
            {
                var state = PendingWorkItems[i];
                state.TryDispose();
            }
            PendingWorkItems.Dispose();
        }
    }
}
