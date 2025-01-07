using System.Diagnostics;
namespace klooie;

public partial class ForLoopLifetime : Recyclable
{
    public List<ForLoopState> forLoopStates = new List<ForLoopState>();
}

public partial class DoLoopLifetime : Recyclable
{
    public List<DoLoopState> doLoopStates = new List<DoLoopState>();
}

public sealed class InnerLoopAPIs
{
    private EventLoop parent;
    private ForLoopLifetime forLifetime;
    private DoLoopLifetime doLifetime;
    private long? pausedTime;

    public bool IsPaused => pausedTime.HasValue;

    internal InnerLoopAPIs(EventLoop parent)
    {
        this.parent = parent;

    }

    internal void Pause()
    {
        pausedTime = Stopwatch.GetTimestamp();
    }

    internal void Resume()
    {
        if (pausedTime == null) return;
        var timePaused = Stopwatch.GetTimestamp() - pausedTime.Value;
        
        if (forLifetime != null)
        {
            for (int i = 0; i < forLifetime.forLoopStates.Count; i++)
            {
                forLifetime.forLoopStates[i].lastIterationTime += timePaused;
            }
        }
        
        if (doLifetime != null)
        {
            for (int i = 0; i < doLifetime.doLoopStates.Count; i++)
            {
                doLifetime.doLoopStates[i].lastIterationTime += timePaused;
            }
        }
        pausedTime = null;
    }

    public void Delay(double delayMs, object scope, Action<object>? then = null) => For(1, delayMs, scope,null, then);
    

    public void Delay(double delayMs, Action? then = null) => For(1, delayMs, null, then);

    public void Do(double delayMs, object scope, Func<object, DoReturnType> action, Action<object>? then = null)
    {
        var loopState = DoLoopStatePool.Instance.Rent();
        loopState.actionO = action;
        loopState.action = null;
        loopState.lastIterationTime = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.state = scope;
        loopState.then = null;
        loopState.thenO = then;
        EnsureDoImplIsRunning(loopState);
    }

    public void Do(double delayMs, Func<DoReturnType> action, Action? then = null)
    {
        var loopState = DoLoopStatePool.Instance.Rent();
        loopState.action = action;
        loopState.actionO = null;
        loopState.lastIterationTime = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.state = null;
        loopState.then = then;
        loopState.thenO = null;
        EnsureDoImplIsRunning(loopState);
    }

 

    public void For(int length, double delayMs, object scope, Action<int, object> action, Action<object>? then = null)
    {
        if (length == 0)
        {
            then?.Invoke(scope);
            return;
        }
        var loopState = ForLoopStatePool.Instance.Rent();
        loopState.length = length;
        loopState.lastIterationTime = 0;
        loopState.i = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.actionO = action;
        loopState.action = null;
        loopState.state = scope;
        loopState.then = null;
        loopState.thenO = then;
        EnsureForImplIsRunning(loopState);
    }

    public void For(int length, double delayMs, Action<int> action, Action? then = null)
    {
        if (length == 0)
        {
            then?.Invoke();
            return;
        }
        var loopState = ForLoopStatePool.Instance.Rent();
        loopState.length = length;
        loopState.lastIterationTime = 0;
        loopState.i = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.action = action;
        loopState.actionO = null;
        loopState.state = null;
        loopState.then = then;
        loopState.thenO = null;
        EnsureForImplIsRunning(loopState);
    }

    private void EnsureDoImplIsRunning(DoLoopState loopState)
    {
        if(doLifetime == null || doLifetime.IsExpired) InitDo();
        doLifetime.doLoopStates.Add(loopState);
    }

    private void EnsureForImplIsRunning(ForLoopState loopState)
    {
        if (forLifetime == null || forLifetime.IsExpired) InitFor();
        forLifetime.forLoopStates.Add(loopState);
    }

    public void InitFor()
    {
        forLifetime = ForLoopLifetimePool.Instance.Rent();
        parent.EndOfCycle.Subscribe(forLifetime, ForImpl, forLifetime);
    }

    public void InitDo()
    {
        doLifetime = DoLoopLifetimePool.Instance.Rent();
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

            var result = loopState.action?.Invoke();
            result = result.HasValue ? result.Value : loopState.actionO?.Invoke(loopState.state);
            if(result.HasValue == false) throw new Exception("Do loop must return a value");

            loopState.lastIterationTime = Stopwatch.GetTimestamp();

            if (result == DoReturnType.Break)
            {
                loopState.thenO?.Invoke(loopState.state);
                loopState.then?.Invoke();
                DoLoopStatePool.Instance.Return(loopState);
                doLoopStates.RemoveAt(i);
                i--;

                if (doLoopStates.Count == 0)
                {
                    DoLoopLifetimePool.Instance.Return(doLifetime);
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

            // Check if it's time to run the next iteration
            var isTimeToIterate = Stopwatch.GetTimestamp() - loopState.lastIterationTime >= loopState.delay;
            if (!isTimeToIterate) continue;

            if (loopState.i < loopState.length)
            {
                // Execute the action for the current iteration
                loopState?.actionO?.Invoke(loopState.i, loopState.state);
                loopState?.action?.Invoke(loopState.i);
                loopState.lastIterationTime = Stopwatch.GetTimestamp();
                loopState.i++;

                // Continue so there is one final delay after the last iteration
                if (loopState.i == loopState.length) continue;
            }

            // Check if the loop has completed
            if (loopState.i == loopState.length)
            {
                loopState.thenO?.Invoke(loopState.state);
                loopState.then?.Invoke();

                // Clean up and remove the loop state
                ForLoopStatePool.Instance.Return(loopState);
                forLoopStates.RemoveAt(iu);
                iu--; // Adjust index after removal

                if (forLoopStates.Count == 0)
                {
                    ForLoopLifetimePool.Instance.Return(forLifetime);
                }
            }
        }
    }
}

public class ForLoopState : Recyclable
{
    public long lastIterationTime;
    public double delay;
    public int i;
    public int length;
    public object state;
    public Action<int, object>? actionO;
    public Action<int>? action;
    public Action<object> thenO;
    public Action then;
}

public class DoLoopState : Recyclable
{
    public long lastIterationTime;
    public double delay;
    public object? state;
    public Func<object, DoReturnType> actionO;
    public Func<DoReturnType>? action;
    public Action<object>? thenO;
    public Action? then;
}

public enum DoReturnType
{
    Continue,
    Break
}
