namespace klooie.Gaming.Code;
public class CatchBlock : CodeBlock
{
    public override string ToString() => $"catch block: {base.ToString()}";

    public override StatementExecutionResult Enter(TimeThread thread)
    {
        base.Enter(thread);
        return base.Exit(thread);
    }
}

