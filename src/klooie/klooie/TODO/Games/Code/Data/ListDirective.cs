using PowerArgs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class ListDirective : Directive
{
    [ArgPosition(0)]
    [ArgRequired]
    public string VariableName { get; set; }

    [ArgPosition(1)]
    [ArgRequired]
    public List<string> Values { get; set; }

    public override Task ExecuteAsync()
    {
        Heap.Current.Set(Values, VariableName);
        return Task.CompletedTask;
    }
}
