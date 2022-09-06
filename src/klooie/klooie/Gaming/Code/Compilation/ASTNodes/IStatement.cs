namespace klooie.Gaming.Code;

public abstract class StatementExecutionResult { }
public class NoOpStatementExecutionResult : StatementExecutionResult { }

public interface IStatement
{
    [ArgIgnore]
    public AST AST { get; set; }
    [ArgIgnore]
    public Block Parent { get; set; }
    [ArgIgnore]
    public List<CodeToken> Tokens { get; set; }
    public abstract StatementExecutionResult Execute(TimeThread thread);
}

public abstract class Statement : IStatement
{
    [ArgIgnore]
    public AST AST { get; set; }
    [ArgIgnore]
    public Block Parent { get; set; }
    [ArgIgnore]
    public List<CodeToken> Tokens { get; set; } = new List<CodeToken>();
    public override string ToString() => string.Join("", Tokens.Select(t => t.Value));
    public abstract StatementExecutionResult Execute(TimeThread thread);
}
