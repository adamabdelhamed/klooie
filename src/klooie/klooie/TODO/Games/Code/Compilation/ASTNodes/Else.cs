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

    public override string ToString()
    {
        var ret = Iterations;
        return $"Else statement that will resolve to  {ret}: {base.ToString()}";
    }
}
