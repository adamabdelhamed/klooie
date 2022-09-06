using klooie.Gaming;
using klooie.Gaming.Code;
using PowerArgs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.Gaming;

public abstract class EventDrivenDirective : Directive
{
    public string DebugId { get; set; }
    public string Until { get; set; }

    public DynamicArg On { get; set; }

    [ArgDefaultValue(0)]
    public virtual DynamicArg Delay { get; set; }

    [ArgDefaultValue(-1)]
    public int MaxExecutions { get; set; } = -1;

    [ArgCantBeCombinedWith(nameof(Continue))]
    public string Then { get; set; }

    [ArgShortcut("c")]
    [ArgCantBeCombinedWith(nameof(Then))]
    public bool Continue { get; set; }
    [ArgIgnore]
    public bool SkipNextContinue { get; set; }

    [ArgCantBeCombinedWith(nameof(If))]
    [ArgRequired(If = nameof(Right) + "|" + nameof(Operator))]
    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public DynamicArg IfLeft { get; set; }



    [ArgCantBeCombinedWith(nameof(If))]
    [ArgRequired(If = nameof(IfLeft) + "|" + nameof(Right))]
    public EvalOperator? Operator { get; set; }


    [ArgCantBeCombinedWith(nameof(If))]
    [ArgRequired(If = nameof(IfLeft) + "|" + nameof(Operator))]
    public DynamicArg Right { get; set; }



    [ArgCantBeCombinedWith(nameof(IfLeft))]
    [ArgCantBeCombinedWith(nameof(Operator))]
    [ArgCantBeCombinedWith(nameof(Right))]
    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public DynamicArg If { get; set; }



    [ArgIgnore]
    public EventDrivenDirective Continuation => Parent?.Statements
        .WhereAs<EventDrivenDirective>()
            .Where(s => s.Source == this.Source && s.Tokens.First().Line == Tokens.First().Line + 1)
            .Select(s => s as EventDrivenDirective)
            .SingleOrDefault();

    private int numberOfExecutions;

    private bool disabledByUntil;
    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if (On?.StringValue == "demand")
        {
            FireNowOrDelay();
        }
        return new NoOpStatementExecutionResult();
    }

    public override async Task ExecuteAsync()
    {

        if (Until != null)
        {
            Game.Current.Subscribe(Until, (ev) =>
            {
                disabledByUntil = true;
            }, Game.Current);
        }

        if (On != null && On?.StringValue != "continue")
        {
            Game.Current.Subscribe(On.StringValue, (ev) => FireNowOrDelay(ev), Game.Current);
        }
        else if (On == null)
        {
            FireNowOrDelay();
        }
    }

    protected virtual float GetEffectiveDelay() => Delay == null ? 0 : Delay.FloatValue;

    public void FireNowOrDelay(GameEvent ev = null)
    {
        object pipedArgs = ev != null ? ev.Args : null;

        if (MaxExecutions > 0 && numberOfExecutions >= MaxExecutions)
        {
            return;
        }

        numberOfExecutions++;

        var effectiveDelay = GetEffectiveDelay();
        if (effectiveDelay == 0)
        {
            FireNow(ev, pipedArgs);
        }
        else
        {
            Game.Current.Invoke(async () =>
            {
                await Game.Current.Delay(effectiveDelay);
                FireNow(ev, pipedArgs);
            });
        }
    }

    public bool IsEnabled()
    {
        If?.Invalidate();
        IfLeft?.Invalidate();
        Right?.Invalidate();

        if (If == null && IfLeft == null)
        {
            return true;
        }
        else if (Evaluator.Evaluate(If?.StringValue, IfLeft?.StringValue, Operator, Right?.StringValue) == false)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    protected bool ShouldRun()
    {
        if (If == null && IfLeft == null)
        {
            return true;
        }
        else if (Evaluator.Evaluate(If?.StringValue, IfLeft?.StringValue, Operator, Right?.StringValue) == false)
        {
            return false;
        }
        return true;
    }

    private void FireNow(GameEvent ev, object pipedArgs)
    {
        if (disabledByUntil)
        {
            return;
        }

        If?.Invalidate();
        IfLeft?.Invalidate();
        Right?.Invalidate();

        if (ShouldRun() == false)
        {
            return;
        }

        Game.Current.Invoke(async () =>
        {
            await OnEventFired(pipedArgs);

            if (Then != null)
            {
                Game.Current.Publish(Then, pipedArgs);
            }
            else if (Continue)
            {
                if (SkipNextContinue)
                {
                    SkipNextContinue = false;
                }
                else
                {
                    var continuationDirective = Continuation;
                    if (continuationDirective != null)
                    {
                        continuationDirective.FireNowOrDelay(ev);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Directive on line {Tokens[0].Line} uses the continue option, but it is not immediately followed by another directive on the next line");
                    }
                }
            }
        });
    }

    public abstract Task OnEventFired(object args);
}
