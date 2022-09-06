using PowerArgs;

namespace klooie.Gaming.Code;
public class IncrementDirective : EventDrivenDirective
{
    [ArgPosition(0)]
    public DynamicArg VariableName { get; set; }

    [ArgPosition(1)]
    [ArgDefaultValue("1")]
    public DynamicArg Amount { get; set; }

    public bool Heap { get; set; }

    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public bool OnlyIfNull { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if(ShouldRun())
        {
            var currentValue = thread.ResolveNumber("{{"+VariableName.StringValue+"}}");
            var newValue = currentValue + Amount.FloatValue;
            return SetFactory(newValue).Execute(thread);
        }
        else
        {
            return new NoOpStatementExecutionResult();
        }
    }

    public override Task OnEventFired(object args)
    {
        if(ShouldRun())
        {
            var currentValue = Evaluator.ToSingle(TimeThread.ResolveStatic("{{" + VariableName + "}}"));
            var newValue = currentValue + Amount.FloatValue;
            return SetFactory(newValue).OnEventFired(args);
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    private SetDirective SetFactory(float newValue) => new SetDirective() { OnlyIfNull = OnlyIfNull, Heap = Heap, VariableName = VariableName, VariableValue = newValue.ToDynamicArg() };
}
