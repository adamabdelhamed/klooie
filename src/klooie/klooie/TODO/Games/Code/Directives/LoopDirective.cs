using PowerArgs;

namespace klooie.Gaming.Code;
public class LoopDirective : Directive
{
    [ArgRequired]
    [ArgDescription("The number of times the program should flow through this directive's target loop")]
    public DynamicArg Iterations { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        var loop = GetClosest<Loop>(false);
        if (loop != null)
        {
            loop.Iterations = Iterations.IntValue;
        }
        return new NoOpStatementExecutionResult();
    }
}
