namespace klooie.Gaming.Code;

public enum EvalOperator
    {
        Equals,
        NotEquals,
        GreaterThanOrEquals,
        GreaterThan,
        LessThan,
        LessThanOrEquals
    }

    public class GameBoolResolver : IBooleanVariableResolver
    {
        public bool ResolveBoolean(string variableName)
        {
            var variableValue = (TimeThread.ResolveStatic(variableName) + "");
            return variableValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }

public class Evaluator
{
    public static IBooleanVariableResolver resolver = new GameBoolResolver();

    public static bool Evaluate(string expression, string left, EvalOperator? op, string right) =>
         expression != null ? EvaluateBooleanExpression(expression) : Evaluate(left, op, right);

    public static bool EvaluateBooleanExpression(string expression) => string.IsNullOrWhiteSpace(expression) ? false : BooleanExpressionParser.Parse(expression).Evaluate(resolver);
    public static bool Evaluate(string leftExpression, EvalOperator? op, string rightExpression)
    {
        if (op.HasValue == false)
        {
            return false;
        }

        var left = TimeThread.ResolveStatic(leftExpression);
        var right = TimeThread.ResolveStatic(rightExpression);

        Func<bool> condition;

        if (op == EvalOperator.Equals)
        {
            condition = () => left.Equals(right);
        }
        else if (op == EvalOperator.NotEquals)
        {
            condition = () => left.Equals(right) == false;
        }
        else if (op == EvalOperator.GreaterThanOrEquals)
        {
            condition = () => ToSingle(left) >= ToSingle(right);
        }
        else if (op == EvalOperator.GreaterThan)
        {
            condition = () => ToSingle(left) > ToSingle(right);
        }
        else if (op == EvalOperator.LessThanOrEquals)
        {
            condition = () => ToSingle(left) <= ToSingle(right);
        }
        else if (op == EvalOperator.LessThan)
        {
            condition = () => ToSingle(left) < ToSingle(right);
        }
        else
        {
            throw new InvalidOperationException("Unknown operator: " + op);
        }

        var eval = condition();
        return eval;
    }

    public static float ToSingle(object o)
    {
        if (o == null)
        {
            return 0f;
        }

        if (o is ConsoleString)
        {
            o = o.ToString();
        }

        if (o is string && (o as string) == string.Empty)
        {
            return 0f;
        }

        return Convert.ToSingle(o);
    }
}
