namespace klooie.Gaming.Code;
public class SetRandomDirective : Directive
{
    private static Random random = new Random();

    [ArgRequired]
    [ArgPosition(0)]
    public string VariableName { get; set; }

    [ArgRequired(IfNot = nameof(Min))]
    [ArgPosition(1)]
    public string ListName { get; set; }

    [ArgRequired(If = nameof(Max))]
    [ArgCantBeCombinedWith(nameof(ListName))]
    public int Min { get; set; }

    [ArgRequired(If = nameof(Min))]
    [ArgCantBeCombinedWith(nameof(ListName))]
    public int Max { get; set; }


    public override StatementExecutionResult Execute(TimeThread thread)
    {
        object value;

        if (ListName != null)
        {
            var list = Heap.Current.Get<List<string>>(ListName);
            value = list[random.Next(0, list.Count)];
        }
        else
        {
            value = random.Next(Min, Max);
        }
        thread.Set(VariableName, value);

        return new NoOpStatementExecutionResult();
    }
}
