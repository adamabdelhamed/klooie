using PowerArgs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public abstract class FunctionDirective : Directive
{
    public string Name { get; set; }

    [ArgIgnore]
    public Function Function { get; private set; }

    public override async Task ExecuteAsync()
    {

        if (Name == null)
        {
            Function = AST.Functions
                .Where(f => f.Tokens.First().Line >= this.Tokens.First().Line)
                .OrderBy(f => Math.Abs(f.Tokens.First().Line - this.Tokens.First().Line))
                .FirstOrDefault();
        }
        else
        {
            Function = AST.Functions
             .Where(f => f.Tokens.Where(t => t.Value == Name).Count() == 1)
             .SingleOrDefault();
        }

        if (Function != null)
        {
            await OnFunctionIdentified(Function);
        }
    }

    protected abstract Task OnFunctionIdentified(Function myFunction);
}