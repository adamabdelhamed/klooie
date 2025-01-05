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
    public InnerLoopAPIs(EventLoop parent)
    {
        this.parent = parent;
    }

    public void Delay(double delayMs, object scope, Action<object>? then = null) => For(1, delayMs, scope, (i, _) => { }, then);
    

    public void Delay(double delayMs, Action? then = null) => For(1, delayMs, (int i) =>{ }, then);

    public void Do(double delayMs, object scope, Func<object, DoReturnType> action, Action<object>? then = null)
    {
        var loopState = DoLoopStatePool.Instance.Rent();
        loopState.action = action;
        loopState.lastIterationTime = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.state = scope;
        loopState.then = then;
        EnsureDoImplIsRunning(loopState);
    }

    public void Do(double delayMs, Func<DoReturnType> action, Action? then = null)
    {
        var loopState = DoLoopStatePool.Instance.Rent();
        loopState.action = (_) => action();
        loopState.lastIterationTime = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.state = null;
        loopState.then = (_) => then?.Invoke();
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
        loopState.action = action;
        loopState.state = scope;
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
        var loopState = ForLoopStatePool.Instance.Rent();
        loopState.length = length;
        loopState.lastIterationTime = 0;
        loopState.i = 0;
        loopState.delay = (double)delayMs * Stopwatch.Frequency / 1000.0;
        loopState.action = (i, _) => action(i);
        loopState.state = null;
        loopState.then = (_) => then?.Invoke();
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

    private static void DoImpl(object loopStates)
    {
        var doLifetime = (DoLoopLifetime)loopStates;
        var doLoopStates = doLifetime.doLoopStates;
        for (int i = 0; i < doLoopStates.Count; i++)
        {
            var loopState = doLoopStates[i];

            var isTimeToIterate = Stopwatch.GetTimestamp() - loopState.lastIterationTime >= loopState.delay;
            if (!isTimeToIterate) continue;

            var result = loopState.action(loopState.state);
            loopState.lastIterationTime = Stopwatch.GetTimestamp();

            if (result == DoReturnType.Break)
            {
                loopState.then?.Invoke(loopState.state);
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


    private static void ForImpl(object loopStates)
    {
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
                loopState.action(loopState.i, loopState.state);
                loopState.lastIterationTime = Stopwatch.GetTimestamp();
                loopState.i++;

                // Continue so there is one final delay after the last iteration
                if (loopState.i == loopState.length) continue;
            }

            // Check if the loop has completed
            if (loopState.i == loopState.length)
            {
                loopState.then?.Invoke(loopState.state);

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
    public Action<int, object> action;
    public Action<object> then;
}

public class DoLoopState : Recyclable
{
    public long lastIterationTime;
    public double delay;
    public object state;
    public Func<object, DoReturnType> action;
    public Action<object> then;
}

public enum DoReturnType
{
    Continue,
    Break
}
