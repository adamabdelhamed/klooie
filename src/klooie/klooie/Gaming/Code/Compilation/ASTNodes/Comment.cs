namespace klooie.Gaming.Code;

public class Comment : Statement
{
    public override StatementExecutionResult Execute(TimeThread thread) =>  new NoOpStatementExecutionResult();
    public override string ToString() => $"Comment: {base.ToString()}";
}

