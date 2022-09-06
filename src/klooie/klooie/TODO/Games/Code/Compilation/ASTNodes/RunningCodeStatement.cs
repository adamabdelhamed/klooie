using System.Text.RegularExpressions;

namespace klooie.Gaming.Code;
public class RunningCodeExecutionResult : StatementExecutionResult
{
    public IStatement Statement { get; set; }
}

public class ThrowResult : StatementExecutionResult
{
    public string Message { get; set; }
    public RunningCodeStatement Statement { get; set; }
}
public delegate Task FunctionImplementation(RunningCodeStatement statement, object[] parameters);

public class RunningCodeStatement : Statement
{
    public AwaitDirective AsyncInfo { get; set; }


    public Task ImplementationTask { get; private set; }
    private static readonly string Whitespace = "\\s*";
    private static readonly string OpenParen = "\\(";
    private static readonly string CloseParen = "\\)";
    private static readonly string Identifier = "[a-zA-Z]+[a-zA-Z0-9]*";
    private static readonly string FunctionName = $"(?<FunctionName>{Identifier})";
    private static readonly string VariableName = $"(?<VariableName>{Identifier})";
    private static readonly string Parameters = $"{OpenParen}(?<Parameters>[^\\)]*){CloseParen}";
    private static readonly string Quote = "\"";
    private static readonly string StringLiteral = $"({Quote}[^{Quote}]*{Quote})";
    private static readonly string NumberLiteral = @"((\d+\.\d+)|(\.\d+)|(\d+))";
    private static readonly string LiteralValue = $"(?<LiteralValue>{StringLiteral}|{NumberLiteral})";
    public static readonly Regex FunctionRegex = new Regex($"^{Whitespace}{FunctionName}{Whitespace}{Parameters}{Whitespace};{Whitespace}$");
    public static readonly Regex AssignmentRegex = new Regex($"^{Whitespace}var{Whitespace}{VariableName}{Whitespace}={Whitespace}{LiteralValue};{Whitespace}$");

    public Func<TimeThread, StatementExecutionResult> OnExecuteHandler { get; set; }

    public override StatementExecutionResult Execute(TimeThread thread)
    {
        StatementExecutionResult ret = new RunningCodeExecutionResult() { Statement = this };
        if (OnExecuteHandler != null)
        {
            ret = OnExecuteHandler.Invoke(thread);
            if (ret is RunningCodeExecutionResult)
            {
                (ret as RunningCodeExecutionResult).Statement = this;
            }
        }

        if (TryParseFunction(out FunctionInfo function))
        {
            var handler = Heap.Current.Get<object>(function.Name + "()") as FunctionImplementation;
            if (handler != null)
            {
                var parameterValues = function.ParameterNames.Select(pName =>
                {
                    if (Regex.IsMatch(pName, StringLiteral))
                    {
                        return DecodeStringLiteral(pName.Substring(1, pName.Length - 2));
                    }
                    else if (Regex.IsMatch(pName, NumberLiteral))
                    {
                        return float.Parse(pName);
                    }
                    else
                    {
                        return thread.Resolve<object>(pName);
                    }
                }).ToArray();
                thread.AsyncTask = new Func<Task>(async () =>
                {
                    await Game.Current.Delay(250);
                    await handler(this, parameterValues);
                })();
            }
        }
        else if (TryParseAssignment(out AssignmentInfo assignment))
        {
            if (Regex.IsMatch(assignment.VariableValue, NumberLiteral))
            {
                thread.Set(assignment.VariableName, float.Parse(assignment.VariableValue));
            }
            else if (Regex.IsMatch(assignment.VariableValue, StringLiteral))
            {
                thread.Set(assignment.VariableName, assignment.VariableValue.Substring(1, assignment.VariableValue.Length - 2));
            }
            else
            {

            }
        }

        if (thread.AsyncTask == null)
        {
            thread.AsyncTask = new Func<Task>(async () =>
            {
                await Delay(thread);
            })();
        }
        return ret;
    }

    private string DecodeStringLiteral(string alreadyStrippedOfQuotes) => alreadyStrippedOfQuotes
        .Replace("\\\\", "\\")
        .Replace("\\\"", "\"")
        .Replace("\\n", "\n")
        .Replace("\\t", "\t");

    private async Task Delay(TimeThread thread)
    {
        if (thread.TryResolve<float>("CPUDelay", out float delay) == false)
        {
            delay = 500;
        }
        await Game.Current.Delay(delay);
    }

    private bool TryParseAssignment(out AssignmentInfo info)
    {
        var code = string.Join("", Tokens.Select(t => t.Value));
        var match = AssignmentRegex.Match(code);
        if (match.Success == false)
        {
            info = null;
            return false;
        }

        info = new AssignmentInfo()
        {
            VariableName = match.Groups["VariableName"].Value,
            VariableValue = match.Groups["LiteralValue"].Value,
        };
        return true;
    }

    private bool TryParseFunction(out FunctionInfo info)
    {
        var code = string.Join("", Tokens.Select(t => t.Value));
        var match = FunctionRegex.Match(code);
        if (match.Success == false)
        {
            info = null;
            return false;
        }

        info = new FunctionInfo();
        info.Name = match.Groups["FunctionName"].Value;

        if (string.IsNullOrWhiteSpace(match.Groups["Parameters"].Value) == false)
        {
            info.ParameterNames = match.Groups["Parameters"].Value
                .Trim()
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => string.IsNullOrWhiteSpace(s) == false)
                .ToArray();
        }
        else
        {
            info.ParameterNames = new string[0];
        }
        return true;
    }

    public override string ToString() => $"RunningCode: {base.ToString()}";
}

public class FunctionHandler
{
    public string Name { get; set; }

    public Func<object[], Task> Implementation { get; set; }
}

public class FunctionInfo
{
    public string Name { get; set; }
    public string[] ParameterNames { get; set; }
}

public class AssignmentInfo
{
    public string VariableName { get; set; }
    public string VariableValue { get; set; }
}