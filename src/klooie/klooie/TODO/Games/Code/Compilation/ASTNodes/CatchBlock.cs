namespace klooie.Gaming.Code;
public class CatchBlock : CodeBlock
{
    public override string ToString()
    {
        return $"catch block: {base.ToString()}";
    }

    public override StatementExecutionResult Enter(TimeThread thread)
    {
        base.Enter(thread);
        return base.Exit(thread);
        //return base.Enter(thread);
    }
}

