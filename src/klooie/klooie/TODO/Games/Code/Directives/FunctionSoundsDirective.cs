using PowerArgs;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class FunctionSoundsDirective : FunctionDirective
{
    [ArgIgnore]
    public Function Function { get; set; }
    public bool IsEnabled { get; set; }

    protected override Task OnFunctionIdentified(Function myFunction)
    {
        Function = myFunction;
        return Task.CompletedTask;
    }

    public static bool IsSoundEnabled(Function f)
    {
        var directivesTargetingFunction = Game.Current
            .Rules
            .WhereAs<FunctionSoundsDirective>()
            .Where(d => d.Function == f)
            .ToArray();

        if (directivesTargetingFunction.Length == 0)
        {
            return true;
        }
        else if (directivesTargetingFunction.Length == 1)
        {
            return directivesTargetingFunction[0].IsEnabled;
        }
        else
        {
            throw new ArgException("Duplicate FunctionSounds directive on function: " + f);
        }
    }
}
