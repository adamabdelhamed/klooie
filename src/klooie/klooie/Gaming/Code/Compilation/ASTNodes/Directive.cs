namespace klooie.Gaming.Code;
public class Directive : Statement, IRule
{
    [ArgIgnore]
    public string Source { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread) => new NoOpStatementExecutionResult();
    public virtual Task ExecuteAsync() => Task.CompletedTask;
    public override string ToString() => $"{GetType().Name}: {base.ToString()}";

    [ArgIgnore]
    public string CommandString => CommandLineSerializer.Serialize(this, new CommandLineArgumentsDefinition(GetType()).Arguments);

    public string Serialize() => GetType() == typeof(Directive) ? "//##" : "//#" + GetType().Name.Replace("Directive", "") + " " + CommandString;
    

    public static bool IsDirective(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("//#") && trimmed.StartsWith("//##") == false;
    }

    public static bool IsDirective<T>(string line)
    {
        if (IsDirective(line) == false) return false;
        var trimmed = line.Trim();
        var prefix = $"//#{typeof(T).Name.Replace("Directive", "")}";
        var ret = trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);
        return ret;
    }

    /// <summary>
    /// Gets the next statement of the given type in the current block or throws if none is found
    /// </summary>
    /// <returns>the next running code statement in the current block or throws if none is found</returns>
    public T GetClosest<T>(bool throwIfNotFound = true) where T : IStatement
    {
        var i = this.Parent.Statements.IndexOf(this);
        var targetStatement = this.Parent.Statements.Skip(i + 1).WhereAs<T>().FirstOrDefault();

        if (targetStatement != null || throwIfNotFound == false)
        {
            return targetStatement;
        }
        else
        {
            throw new ArgException("Could not find target statement");
        }
    }
}
