using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class DisableFunctionDirective : EventDrivenDirective
{
    private Function myFunction;
    private List<CodeControl> myCode;
    public bool DisposeOnDisabled { get; set; }
    public override Task OnEventFired(object args)
    {
        myFunction = AST.Functions
            .Where(f => f.Tokens.First().Line >= this.Tokens.First().Line)
            .OrderBy(f => Math.Abs(f.Tokens.First().Line - this.Tokens.First().Line))
            .FirstOrDefault();

        if (myFunction == null)
        {
            throw new ArgException("There was no function below this directive");
        }

        myFunction.CanRun = false;


        myCode = Game.Current.GamePanel.Controls
            .WhereAs<CodeControl>()
            .Where(c => c.Token != null && c.Token.IsWithin(myFunction))
            .OrderBy(c => c.Token.Line)
            .ThenBy(c => c.Token.Column)
            .ToList();

        foreach (var c in myCode)
        {
            if (DisposeOnDisabled)
            {
                c.Dispose();
            }
            else
            {
                c.IsDimmed = true;
            }
        }

        foreach (var t in Process.Current.Threads.Where(f => f.Options.EntryPoint == myFunction))
        {
            t.Dispose();
        }

        return Task.CompletedTask;
    }
}
