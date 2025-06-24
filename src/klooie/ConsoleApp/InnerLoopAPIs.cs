using System.Diagnostics;
namespace klooie;

public partial class ForLoopLifetime : Recyclable
{
    public List<ForLoopStateBase> forLoopStates;

    public ForLoopLifetime()
    {
        forLoopStates = new List<ForLoopStateBase>();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        foreach (var loopState in forLoopStates)
        {
            loopState.TryDispose();
        }
        forLoopStates.Clear();
    }
}

public partial class DoLoopLifetime : Recyclable
{
    public List<DoLoopStateBase> doLoopStates;

    public DoLoopLifetime()
    {
        doLoopStates = new List<DoLoopStateBase>();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        foreach (var loopState in doLoopStates)
        {
            loopState.TryDispose();
        }
        doLoopStates.Clear();
    }
}

public sealed class InnerLoopAPIs
{
    private const int MaxForLoopInvocationsPerSecond = 1000;
    private EventLoop parent;
    private ForLoopLifetime forLifetime;
    private int forLease;
    private DoLoopLifetime doLifetime;
    private int doLease;
    private long? pausedTime;

    public bool IsPaused => pausedTime.HasValue;

    internal InnerLoopAPIs(EventLoop parent)
    {
        this.parent = parent;
        parent.OnDisposed(Cleanup);
    }

    private void Cleanup()
    {
        if(forLifetime?.IsStillValid(forLease) == true) forLifetime.Dispose();
        if (doLifetime?.IsStillValid(doLease) == true) doLifetime.Dispose();
        forLifetime = null;
        doLifetime = null;
    }

    internal void Pause()
    {
        pausedTime = Stopwatch.GetTimestamp();
    }

    internal void Resume()
    {
        if (pausedTime == null) return;
        var timePaused = Stopwatch.GetTimestamp() - pausedTime.Value;

        if (forLifetime != null && forLifetime.IsStillValid(forLease))
        {
            for (int i = 0; i < forLifetime.forLoopStates.Count; i++)
            {
                forLifetime.forLoopStates[i].lastIterationTime += timePaused;
            }
        }

        if (doLifetime != null && doLifetime.IsStillValid(doLease))
        {
            for (int i = 0; i < doLifetime.doLoopStates.Count; i++)
            {
                doLifetime.doLoopStates[i].lastIterationTime += timePaused;
            }
        }

        pausedTime = null;
    }

    private List<DelayState>? delayStates;
    public void DelayIfValid<TState>(double delayMs, TState statefulScope, Action<TState> then) where TState : DelayState
    {
        statefulScope.InnerAction = o => then((TState)o);

        if(delayStates == null)
        {
            delayStates = new List<DelayState>();
            ConsoleApp.Current.OnDisposed(this, DisposeStates);
        }
        delayStates.Add(statefulScope);
        Delay(delayMs, statefulScope, InvokeDelayCallbackIfAllDependenciesAreValid);
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
        var _this = (InnerLoopAPIs)innerLoopObs;
        if (_this.delayStates == null) return;
        foreach (var ds in _this.delayStates)
        {
            ds.TryDispose();
        }
        _this.delayStates.Clear();
        _this.delayStates = null;
    }

    private static void InvokeDelayCallbackIfAllDependenciesAreValid(DelayState ds)
    {
        var state = ds;
        if (state.AreAllDependenciesValid == false)
        {
            state.TryDispose();
            return;
        }
        state.InnerAction?.Invoke(ds);
    }

    public void Delay<TScope>(double delayMs, TScope scope, Action<TScope>? then = null)
        => For(1, delayMs, scope, null, then);

    public void Delay(double delayMs, Action? then = null) => For(1, delayMs, null, then);

    public void Do<TScope>(double delayMs, TScope scope, Func<TScope, DoReturnType> action, Action<TScope>? then = null)
    {
        var loopState = DoLoopState<TScope>.Rent(out _);
        loopState.lastIterationTime = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.scope = scope;
        loopState.action = action;
        loopState.then = then;
        EnsureDoImplIsRunning(loopState);
    }

    public void Do(double delayMs, Func<DoReturnType> action, Action? then = null)
    {
        var loopState = DoLoopState.Rent(out _);
        loopState.action = action;
        loopState.lastIterationTime = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.then = then;
        EnsureDoImplIsRunning(loopState);
    }

    public void For<TScope>(int length, double delayMs, TScope scope, Action<int, TScope> action, Action<TScope>? then = null)
    {
        if (length == 0)
        {
            then?.Invoke(scope);
            return;
        }
        var loopState = ForLoopState<TScope>.Rent(out _);
        loopState.length = length;
        loopState.lastIterationTime = 0;
        loopState.i = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.action = action;
        loopState.scope = scope;
        loopState.then = then;
        EnsureForImplIsRunning(loopState);
    }

    public void For(int length, double delayMs, Action<int> action, Action? then = null)
    {
        if (length == 0)
        {
            then?.Invoke();
            return;
        }
        var loopState = ForLoopState.Rent(out _);
        loopState.length = length;
        loopState.lastIterationTime = 0;
        loopState.i = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.action = action;
        loopState.then = then;
        EnsureForImplIsRunning(loopState);
    }

    private void EnsureDoImplIsRunning(DoLoopStateBase loopState)
    {
        if (doLifetime == null || !doLifetime.IsStillValid(doLease)) InitDo();
        doLifetime.doLoopStates.Add(loopState);
    }

    private void EnsureForImplIsRunning(ForLoopStateBase loopState)
    {
        if (forLifetime == null || !forLifetime.IsStillValid(forLease)) InitFor();
        forLifetime.forLoopStates.Add(loopState);
    }

    public void InitFor()
    {
        forLifetime = ForLoopLifetimePool.Instance.Rent(out forLease);
        parent.EndOfCycle.SubscribeThrottled(forLifetime, ForImpl, forLifetime, MaxForLoopInvocationsPerSecond);
    }

    public void InitDo()
    {
        doLifetime = DoLoopLifetimePool.Instance.Rent(out doLease);
        parent.EndOfCycle.Subscribe(doLifetime, DoImpl, doLifetime);
    }

    private void DoImpl(object loopStates)
    {
        if (pausedTime.HasValue) return;
        var doLifetime = (DoLoopLifetime)loopStates;
        var doLoopStates = doLifetime.doLoopStates;
        for (int i = 0; i < doLoopStates.Count; i++)
        {
            var loopState = doLoopStates[i];

            var isTimeToIterate = Stopwatch.GetTimestamp() - loopState.lastIterationTime >= loopState.delay;
            if (!isTimeToIterate) continue;

            var result = loopState.Execute();
            if (result.HasValue == false) throw new Exception("Do loop must return a value");

            loopState.lastIterationTime = Stopwatch.GetTimestamp();

            if (result == DoReturnType.Break)
            {
                loopState.InvokeThen();
                loopState.Dispose();
                doLoopStates.RemoveAt(i);
                i--;

                if (doLoopStates.Count == 0)
                {
                    doLifetime.Dispose();
                }
            }
        }
    }

    private void ForImpl(object loopStates)
    {
        if (pausedTime.HasValue) return;
        var forLifetime = (ForLoopLifetime)loopStates;
        var forLoopStates = forLifetime.forLoopStates;
        for (int iu = 0; iu < forLoopStates.Count; iu++)
        {
            var loopState = forLoopStates[iu];

            var isTimeToIterate = Stopwatch.GetTimestamp() - loopState.lastIterationTime >= loopState.delay;
            if (!isTimeToIterate) continue;

            if (loopState.i < loopState.length)
            {
                loopState.InvokeAction();
                loopState.lastIterationTime = Stopwatch.GetTimestamp();
                loopState.i++;

                if (loopState.i == loopState.length) continue;
            }

            if (loopState.i == loopState.length)
            {
                loopState.InvokeThen();
                loopState.Dispose();
                forLoopStates.RemoveAt(iu);
                iu--;

                if (forLoopStates.Count == 0)
                {
                    forLifetime.Dispose();
                }
            }
        }
    }
}

public abstract class ForLoopStateBase : Recyclable
{
    public long lastIterationTime;
    public double delay;
    public int i;
    public int length;
    public abstract void InvokeAction();
    public abstract void InvokeThen();
}

public class ForLoopState : ForLoopStateBase
{
    public Action<int>? action;
    public Action? then;

    public override void InvokeAction() => action?.Invoke(i);
    public override void InvokeThen() => then?.Invoke();
    protected override void OnReturn()
    {
        base.OnReturn();
        action = null;
        then = null;
    }

    private sealed class Pool : RecycleablePool<ForLoopState>
    {
        public static Pool? _instance;
        public static Pool Instance => _instance ??= new Pool();
        public override ForLoopState Factory() => new ForLoopState();
    }
    public static ForLoopState Rent(out int lease) => Pool.Instance.Rent(out lease);
}

public class ForLoopState<TScope> : ForLoopStateBase
{
    public TScope scope;
    public Action<int, TScope>? action;
    public Action<TScope>? then;

    public override void InvokeAction() => action?.Invoke(i, scope);
    public override void InvokeThen() => then?.Invoke(scope);

    protected override void OnReturn()
    {
        base.OnReturn();
        action = null;
        then = null;
        scope = default;
    }

    private sealed class Pool : RecycleablePool<ForLoopState<TScope>>
    {
        public static Pool? _instance;
        public static Pool Instance => _instance ??= new Pool();
        public override ForLoopState<TScope> Factory() => new ForLoopState<TScope>();
    }
    public static ForLoopState<TScope> Rent(out int lease) => Pool.Instance.Rent(out lease);
}

public abstract class DoLoopStateBase : Recyclable
{
    public long lastIterationTime;
    public double delay;
    public abstract DoReturnType? Execute();
    public abstract void InvokeThen();
}

public class DoLoopState : DoLoopStateBase
{
    public Func<DoReturnType>? action;
    public Action? then;

    public override DoReturnType? Execute() => action?.Invoke();
    public override void InvokeThen() => then?.Invoke();

    protected override void OnReturn()
    {
        base.OnReturn();
        action = null;
        then = null;
    }

    private sealed class Pool : RecycleablePool<DoLoopState>
    {
        public static Pool? _instance;
        public static Pool Instance => _instance ??= new Pool();
        public override DoLoopState Factory() => new DoLoopState();
    }
    public static DoLoopState Rent(out int lease) => Pool.Instance.Rent(out lease);
}

public class DoLoopState<TScope> : DoLoopStateBase
{
    public TScope scope;
    public Func<TScope, DoReturnType> action;
    public Action<TScope>? then;

    public override DoReturnType? Execute() => action?.Invoke(scope);
    public override void InvokeThen() => then?.Invoke(scope);

    protected override void OnReturn()
    {
        base.OnReturn();
        action = null;
        then = null;
        scope = default;
    }

    private sealed class Pool : RecycleablePool<DoLoopState<TScope>>
    {
        public static Pool? _instance;
        public static Pool Instance => _instance ??= new Pool();
        public override DoLoopState<TScope> Factory() => new DoLoopState<TScope>();
    }
    public static DoLoopState<TScope> Rent(out int lease) => Pool.Instance.Rent(out lease);
}

public enum DoReturnType
{
    Continue,
    Break
}
