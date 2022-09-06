namespace klooie.Gaming.Code;
public class If : Loop
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

    public override string ToString() => $"If statement that will resolve to a loop with iterations: {Iterations}: {base.ToString()}";

    public IEnumerable<CodeToken> GetTokensFromIfToOpenCurlyTrimmed()
    {
        var hitIf = false;
        for (var i = 0; i < Tokens.Count; i++)
        {
            if (hitIf == false && Tokens[i].Value == "if")
            {
                hitIf = true;
                yield return Tokens[i];
            }
            else if (hitIf == false)
            {
                // ignore
            }
            else if (Tokens[i] == OpenCurly)
            {
                break;
            }
            else
            {
                yield return Tokens[i];
            }
        }
    }
}
