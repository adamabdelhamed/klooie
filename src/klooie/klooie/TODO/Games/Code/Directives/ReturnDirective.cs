using PowerArgs;

namespace klooie.Gaming.Code;
public class ReturnDirective : Directive
{
    public const string ReturnValueKey = "returnValue";

    [ArgRequired]
    [ArgPosition(0)]
    public DynamicArg Value { get; set; }

    public string Then { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        Heap.Current.Set(Value.ObjectValue, ReturnValueKey);
        Game.Current.Publish(Then, thread);
        return base.Execute(thread);
    }
}
