using PowerArgs;
namespace klooie.Gaming.Code;
public class IfDirective : Directive
{
    [ArgCantBeCombinedWith(nameof(Expression))]
    public string Left { get; set; }
    [ArgCantBeCombinedWith(nameof(Expression))]
    public EvalOperator Operator { get; set; }
    [ArgCantBeCombinedWith(nameof(Expression))]
    public string Right { get; set; }

    [ArgCantBeCombinedWith(nameof(Left))]
    [ArgCantBeCombinedWith(nameof(Operator))]
    [ArgCantBeCombinedWith(nameof(Right))]
    public string Expression { get; set; }

    public string OnTrue { get; set; }

    public string OnFalse { get; set; }

    [ArgIgnore]
    public bool CurrentValue { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        CurrentValue = Evaluator.Evaluate(Expression, Left, Operator, Right);

        if (CurrentValue && OnTrue != null)
        {
            Game.Current.Publish(OnTrue);
        }

        if (!CurrentValue && OnFalse != null)
        {
            Game.Current.Publish(OnFalse);
        }

        return new NoOpStatementExecutionResult();
    }
}
