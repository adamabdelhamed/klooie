using PowerArgs;

using System;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class IncrementDirective : EventDrivenDirective
{
    [ArgPosition(0)]
    public string VariableName { get; set; }

    [ArgPosition(1)]
    [ArgDefaultValue("1")]
    public DynamicArg Amount { get; set; }

    private string currentEventId;
    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if (ShouldRun())
        {
            if (On == null || On?.StringValue == "demand" || currentEventId == On?.StringValue)
            {
                var currentValue = Convert.ToSingle(TimeThread.ResolveStatic("{{" + VariableName + "}}").ToString());
                thread.Set(VariableName, currentValue + Amount.FloatValue);
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
            var currentValue = Evaluator.ToSingle(TimeThread.ResolveStatic("{{" + VariableName + "}}"));
            Game.Current.RuleVariables.Set(currentValue + Amount.FloatValue, VariableName);
        }
        return Task.CompletedTask;
    }
}
