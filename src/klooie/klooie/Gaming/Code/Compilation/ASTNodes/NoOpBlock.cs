namespace klooie.Gaming.Code;
public class NoOpBlock : Block
{
    public override StatementExecutionResult Execute(TimeThread thread) { return new NoOpStatementExecutionResult(); }

    public override string ToString()
    {
        return $"NoOpBlock: {base.ToString()}";
    }
}

