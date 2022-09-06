using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class ScriptDirective : Directive
{
    [ArgRequired]
    public DynamicArg Id { get; set; }
    public override Task ExecuteAsync()
    {
        throw new InvalidOperationException("Scripts should be replaced");
    }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        throw new InvalidOperationException("Scripts should be replaced");
    }
}
