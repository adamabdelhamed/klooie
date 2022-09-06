using PowerArgs;
namespace klooie.Gaming.Code;

public class BlockEnteredExecutionResult : StatementExecutionResult
{
    public Block Block { get; set; }
}

public class BlockExitedExecutionResult : StatementExecutionResult
{
    public Block Block { get; set; }
}

public abstract class Block : Statement
{
    internal string CurrentStatementIndexAddress => this.Path + "/statementIndex";
    private string InitializedAddress => this.Path + "/initialized";
    protected bool IsInitialized(TimeThread thread) => thread.IsInScope(InitializedAddress);

    public string Path { get; set; }
    public int Id { get; set; }
    public int Depth { get; set; }
    public CodeToken OpenCurly => Tokens.Where(t => t.Value == "{").FirstOrDefault();
    public CodeToken CloseCurly => Tokens.Where(t => t.Value == "}").LastOrDefault();
    public List<IStatement> Statements { get; private set; } = new List<IStatement>();


    public virtual StatementExecutionResult Enter(TimeThread thread)
    {
        thread.Options.Log.Fire($"{ToString()} entered".ToConsoleString());
        thread.CallStack.Push(new CallStackFrame()
        {
            Statement = this,
        });
        thread.Set(InitializedAddress, true);
        return new BlockEnteredExecutionResult() { Block = this };
    }

    public virtual StatementExecutionResult Exit(TimeThread thread)
    {
        thread.CallStack.Pop();
        thread.Options.Log.Fire($"{ToString()} exited".ToConsoleString());
        return new BlockExitedExecutionResult() { Block = this };
    }


    public void Visit(Func<IStatement, bool> visitor)
    {
        if (visitor(this))
        {
            return;
        }

        foreach (var statement in Statements.ToArray())
        {
            if (statement is Block)
            {
                (statement as Block).Visit(visitor);
            }
            else
            {
                if (visitor(statement))
                {
                    return;
                }
            }
        }
    }
    /*
    public void PurgeNoOps()
    {
        for (var i = 0; i < Statements.Count; i++)
        {
            var statement = Statements[i];

            if (statement is NoOpStatement)
            {
                Statements.RemoveAt(i--);
            }
            else if (statement is Block)
            {
                (statement as Block).PurgeNoOps();

                if (statement is NoOpBlock && (statement as NoOpBlock).Statements.Count == 0)
                {
                    Statements.RemoveAt(i--);
                }
            }
        }
    }*/
}
