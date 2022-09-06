namespace klooie.Gaming.Code;
public class NoOpBlock : Block
{
    public override StatementExecutionResult Execute(TimeThread thread) => new NoOpStatementExecutionResult();
    public override string ToString() => $"NoOpBlock: {base.ToString()}";
}

