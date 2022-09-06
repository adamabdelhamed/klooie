namespace klooie.Gaming.Code;
public class NoOpStatement : Statement
{
    public override StatementExecutionResult Execute(TimeThread thread) { return new NoOpStatementExecutionResult(); }
    public override string ToString() => $"NoOpStatement: {base.ToString()}";
}
