namespace klooie.Gaming.Code;
public class ReturnStatementResult : StatementExecutionResult
{
    public Return Statement { get; set; }
}

public class Return : RunningCodeStatement
{
    public override StatementExecutionResult Execute(TimeThread thread)
    {
        thread.Options.Log.Fire($"Return - {ToString()}".ToConsoleString());
        base.Execute(thread);
        var current = Parent;
        while (current is Function == false)
        {
            if(current == null) throw new NotSupportedException("Return statements must exist within functions");
            current = current.Parent;
        }

        (current as Function).Exit(thread);
        return new ReturnStatementResult() { Statement = this };
    }
}
