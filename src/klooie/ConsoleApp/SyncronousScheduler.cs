using System.Diagnostics;
namespace klooie;

public sealed class SyncronousScheduler
{
    private const int MaxForLoopInvocationsPerSecond = 1000;
    private EventLoop parent;
    private LeaseState<SchedulerLoopLifetime> schedulerLoopLease;
    private long? pausedTime;
    private List<DelayState>? delayStates;
    public bool IsPaused => pausedTime.HasValue;

    internal SyncronousScheduler(EventLoop parent)
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
        var timePaused = Stopwatch.GetTimestamp() - pausedTime.Value;

        if (schedulerLoopLease?.IsRecyclableValid == true)
        {
            for (int i = 0; i < schedulerLoopLease.Recyclable!.PendingWorkItems.Count; i++)
            {
                schedulerLoopLease.Recyclable!.PendingWorkItems[i].TimeAddedToSchedule += timePaused;
            }
        }

        pausedTime = null;
    }


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

    private static void InvokeDelayCallbackIfAllDependenciesAreValid<TState>(DelayIfValidInstance<TState> delayIfValidInstance) where TState : DelayState
    {
        try
        {
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


    public void Delay<TScope>(double delayMs, TScope state, Action<TScope> callback) => EnsureDelayLoopIsRunning(StatefulWorkItem<TScope>.Create(state, callback, delayMs));
    public void Delay(double delayMs, Action callback) => EnsureDelayLoopIsRunning(StatelessWorkItem.Create(callback, delayMs));

    private void EnsureDelayLoopIsRunning(ScheduledWorkItem loopState)
    {
        if (schedulerLoopLease == null || !schedulerLoopLease.IsRecyclableValid) InitDelayLoop();
        schedulerLoopLease.Recyclable!.PendingWorkItems.Items.Add(loopState);
    }

    public void InitDelayLoop()
    {
        var loopLifetime = SchedulerLoopLifetime.Create();
        schedulerLoopLease = LeaseHelper.Track(loopLifetime);
        parent.EndOfCycle.SubscribeThrottled(loopLifetime, Process, loopLifetime, MaxForLoopInvocationsPerSecond);
    }

    private void Process(SchedulerLoopLifetime loopLifetime)
    {
        if (pausedTime.HasValue) return;
        var pendingDelayStates = loopLifetime.PendingWorkItems;
        for (int i = 0; i < pendingDelayStates.Count; i++)
        {
            var delayState = pendingDelayStates.Items[i];

            var isTimeToIterate = Stopwatch.GetTimestamp() - delayState.TimeAddedToSchedule >= delayState.DelayTicks;
            if (!isTimeToIterate) continue;

            delayState.InvokeCallback();
            delayState.Dispose();
            pendingDelayStates.Items.RemoveAt(i);
            i--;

            if (pendingDelayStates.Count == 0)
            {
                loopLifetime.Dispose();
            }
        }
    }

    private class DelayIfValidInstance<TState> : Recyclable
       where TState : DelayState
    {
        public Action<TState> Callback { get; set; }
        public TState DelayState { get; set; }


        private static LazyPool<DelayIfValidInstance<TState>> pool = new LazyPool<DelayIfValidInstance<TState>>(() => new DelayIfValidInstance<TState>());
        private DelayIfValidInstance() { }
        public static DelayIfValidInstance<TState> Create(Action<TState> callback, TState state)
        {
            var instance = pool.Value.Rent();
            instance.Callback = callback;
            instance.DelayState = state;
            return instance;
        }
        protected override void OnReturn()
        {
            base.OnReturn();
            Callback = null;
            DelayState = null;
        }
    }

    private abstract class ScheduledWorkItem : Recyclable
    {
        public double DelayTicks { get; protected set; }
        internal double TimeAddedToSchedule;
        public abstract void InvokeCallback();
    }

    private class StatelessWorkItem : ScheduledWorkItem
    {
        private Action Callback;
        private static LazyPool<StatelessWorkItem> pool = new LazyPool<StatelessWorkItem>(() => new StatelessWorkItem());
        private StatelessWorkItem() { }
        public static StatelessWorkItem Create(Action callback, double delay)
        {
            var instance = pool.Value.Rent();
            instance.Callback = callback ?? throw new ArgumentNullException(nameof(callback), "Callback cannot be null");
            instance.DelayTicks = delay * Stopwatch.Frequency / 1000.0;
            instance.TimeAddedToSchedule = Stopwatch.GetTimestamp();
            return instance;
        }
        public override void InvokeCallback() => Callback?.Invoke();
        protected override void OnReturn()
        {
            base.OnReturn();
            Callback = null;
        }
    }

    private class StatefulWorkItem<TScope> : ScheduledWorkItem
    {
        private TScope State;
        private Action<TScope>? Callback;
        public override void InvokeCallback() => Callback?.Invoke(State);
        private StatefulWorkItem() { }

        private static LazyPool<StatefulWorkItem<TScope>> pool = new LazyPool<StatefulWorkItem<TScope>>(() => new StatefulWorkItem<TScope>());
        public static StatefulWorkItem<TScope> Create(TScope state, Action<TScope> callback, double delay)
        {
            var instance = pool.Value.Rent();
            instance.State = state ?? throw new ArgumentNullException(nameof(state), "State cannot be null");
            instance.Callback = callback ?? throw new ArgumentNullException(nameof(callback), "Callback cannot be null");
            instance.DelayTicks = delay * Stopwatch.Frequency / 1000.0;
            instance.TimeAddedToSchedule = Stopwatch.GetTimestamp();
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