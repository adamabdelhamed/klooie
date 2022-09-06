namespace klooie.Gaming.Code;

public class Loop : Function
{
    public int Iterations { get; set; }

    protected string IterationsAddress => this.Path + "/iterations";

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if (base.IsInitialized(thread) == false)
        {
            var baseEnterResult = Enter(thread);

            if (baseEnterResult is BlockExitedExecutionResult)
            {
                return baseEnterResult;
            }

            if (Iterations > 0)
            {
                thread.Set(IterationsAddress, Iterations);
                return baseEnterResult;
            }
            else
            {
                return Exit(thread);
            }
        }
        else
        {
            return base.Execute(thread);
        }
    }

    public override StatementExecutionResult Exit(TimeThread thread)
    {
        if (thread.TryResolve<int>(IterationsAddress, out int iterationsRemaining))
        {
            iterationsRemaining = iterationsRemaining - 1;
        }
        else
        {
            iterationsRemaining = 0;
        }

        if (iterationsRemaining > 0)
        {
            thread.Options.Log.Fire($"Loop {ToString()} iterating with {iterationsRemaining} iterations remaining".ToConsoleString());
            thread.Set(IterationsAddress, iterationsRemaining);
            thread.Set(CurrentStatementIndexAddress, 0);
            return new LoopIteratedExecutionResult() { Loop = this };
        }
        else
        {
            return base.Exit(thread);
        }
    }

    public override string ToString() => $"Loop with {Iterations} iterations: {base.ToString()}";
}

public class LoopIteratedExecutionResult : StatementExecutionResult
{
    public Loop Loop { get; set; }
}