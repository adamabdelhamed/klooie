namespace klooie.Gaming.Code;
public class Else : Loop
{
    public override StatementExecutionResult Execute(TimeThread thread)
    {
        if (IsInitialized(thread) == false)
        {
            Iterations = 0;
            var maxDirectiveIndex = Parent.Statements.IndexOf(this) - 1;
            for (var i = maxDirectiveIndex; i >= 0; i--)
            {
                if (Parent.Statements[i] is IfDirective)
                {
                    Iterations = (Parent.Statements[i] as IfDirective).CurrentValue ? 1 : 0;
                    break;
                }
            }
        }

        return base.Execute(thread);
    }

    public override string ToString() => $"Else statement that will resolve to a loop with Iterations == {Iterations}: {base.ToString()}";
}
