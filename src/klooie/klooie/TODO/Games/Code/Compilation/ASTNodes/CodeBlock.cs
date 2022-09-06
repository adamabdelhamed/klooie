using PowerArgs;
using System.Collections.Generic;
using System.Linq;
namespace klooie.Gaming.Code;
public class CodeBlock : Block
{
    private List<IStatement> ExecutableStatements
    {
        get
        {
            return Statements.Where(s => s is NoOpStatement == false && s is NoOpBlock == false).ToList();
        }
    }

    public override StatementExecutionResult Enter(TimeThread thread)
    {
        var ret = base.Enter(thread);
        thread.Set(CurrentStatementIndexAddress, 0);

        if (ExecutableStatements.Count == 0)
        {
            return Exit(thread);
        }
        else
        {
            return ret;
        }
    }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        int statementIndex;

        if (base.IsInitialized(thread) == false)
        {
            return Enter(thread);
        }
        else if ((statementIndex = thread.Resolve<int>(CurrentStatementIndexAddress)) < ExecutableStatements.Count)
        {
            thread.Set(CurrentStatementIndexAddress, statementIndex + 1);
            var ret = ExecutableStatements[statementIndex].Execute(thread);
            return ret;
        }
        else
        {
            return Exit(thread);
        }
    }
}
