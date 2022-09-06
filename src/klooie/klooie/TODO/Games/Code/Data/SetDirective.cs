using PowerArgs;

namespace klooie.Gaming.Code;
public class SetDirective : EventDrivenDirective
{
    [ArgPosition(0)]
    public DynamicArg VariableName { get; set; }

    [ArgPosition(1)]
    [ArgRequired]
    public DynamicArg VariableValue { get; set; }

    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public bool OnlyIfNull { get; set; }

    public bool Heap { get; set; }

    private string currentEventId;


    private object ResolvedValue
    {
        get
        {
            var val = VariableValue.ObjectValue;

            if ((val is string || val is ConsoleString) && float.TryParse(val.ToString(), out float newVal))
            {
                val = newVal;
            }
            return val;
        }
    }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if (ShouldRun())
        {
            if (On == null || On.StringValue == "demand" || currentEventId == On.StringValue)
            {
                if (Heap)
                {
                    SetHeap();
                }
                else
                {
                    SetThread(thread);
                }
            }
        }
        return new NoOpStatementExecutionResult();
    }

    public override Task OnEventFired(object args)
    {
        var thread = args as TimeThread ?? TimeThread.Current;
        if (thread != null)
        {
            currentEventId = On?.StringValue;
            try
            {
                Execute(thread);
            }
            finally
            {
                currentEventId = null;
            }
        }
        else
        {
            SetHeap();
        }
        return Task.CompletedTask;
    }

    private void SetHeap()
    {
        if (OnlyIfNull == false || Game.Current.RuleVariables.TryGetValue(VariableName.StringValue, out object _) == false)
        {
            klooie.Gaming.Code.Heap.Current.Set(ResolvedValue, VariableName.StringValue);
        }
    }

    private void SetThread(TimeThread thread)
    {
        if (OnlyIfNull == false || thread.TryResolve(VariableName.StringValue, out object _) == false)
        {
            thread.Set(VariableName.StringValue, ResolvedValue);
        }
    }
}
