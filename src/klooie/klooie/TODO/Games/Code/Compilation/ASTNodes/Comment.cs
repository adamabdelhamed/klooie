namespace klooie.Gaming.Code;

public class Comment : Statement
{
    public override StatementExecutionResult Execute(TimeThread thread) { return new NoOpStatementExecutionResult(); }

    public override string ToString()
    {
        return $"Comment: {base.ToString()}";
    }
}

