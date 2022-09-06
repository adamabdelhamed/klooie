namespace klooie.Gaming.Code;
public class ThrowIfDirective : Directive
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

    [ArgRequired]
    public string Message { get; set; }

    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public string OnThrow { get; set; }

    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public string OnNotThrow { get; set; }

    public DisabledUntilArgument DisabledUntil { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if (DisabledUntil != null && DisabledUntil.AllowExecute() == false)
        {
            return new NoOpStatementExecutionResult();
        }

        var targetStatement = GetClosest<RunningCodeStatement>();
        var shouldThrow = Evaluator.Evaluate(Expression, Left, Operator, Right);

        if (shouldThrow)
        {
            if (OnThrow != null)
            {
                Game.Current.Publish(OnThrow);
            }
            return new ThrowResult() { Message = Message, Statement = targetStatement };
        }
        else
        {
            if (OnNotThrow != null)
            {
                Game.Current.Publish(OnNotThrow);
            }

            return new NoOpStatementExecutionResult();
        }
    }
}
