using PowerArgs;
using System.Threading.Tasks;

namespace klooie.Gaming.Code;
public class CodeHPDirective : EventDrivenDirective
{
    [ArgDefaultValue(10)]
    public DynamicArg HP { get; set; } = DynamicArg.FromObject(10);

    public CodeHPDirective()
    {
        On = DynamicArg.FromObject(Game.ReadyEventId);
    }

    public override Task OnEventFired(object args)
    {
        foreach (var el in Game.Current.GamePanel.Controls.WhereAs<CodeControl>())
        {
            el.Power.HP = HP.FloatValue;
            el.Power.MaxHP = HP.FloatValue;
        }

        Game.Current.GamePanel.Controls.Added.Subscribe((el) =>
        {
            if (el is CodeControl)
            {
                var c = el as CodeControl;
                c.Power.HP = HP.FloatValue;
                c.Power.MaxHP = HP.FloatValue;
            }
        }, Game.Current);
        return Task.CompletedTask;
    }
}
