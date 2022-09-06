namespace klooie.Gaming.Code;
public class ContainsDirective : Directive
{
    [ArgPosition(0)]
    [ArgRequired]
    public string VariableName { get; set; }

    [ArgPosition(1)]
    [ArgRequired]
    public DynamicArg TestValue { get; set; }

    [ArgRequired]
    public string Then { get; set; }
    public override StatementExecutionResult Execute(TimeThread thread)
    {
        var list = thread.Resolve<List<string>>(VariableName);
        if (list.Contains(TestValue.StringValue))
        {
            Game.Current.Publish(Then, thread);
        }
        return new NoOpStatementExecutionResult();
    }
}
