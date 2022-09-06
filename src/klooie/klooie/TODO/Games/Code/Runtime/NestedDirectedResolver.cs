using PowerArgs;
using System.Collections;
namespace klooie.Gaming.Code;
public static class NestedDirectedResolver
    {
        private static NestedDirectiveModel model = new NestedDirectiveModel();

        public static object Resolve(string nestedCommand)
        {
            if (nestedCommand.StartsWith("{") == false || nestedCommand.EndsWith("}") == false)
            {
                throw new ArgumentException("value must start with '{' and end with '}'");
            }

            var tokens = nestedCommand.Substring(1, nestedCommand.Length - 2).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens[0].Equals("multiply", StringComparison.OrdinalIgnoreCase))
            {
                return model.Multiply(tokens);
            }
            else if (tokens[0].Equals("divide", StringComparison.OrdinalIgnoreCase))
            {
                return model.Divide(tokens);
            }
            else if (tokens[0].Equals("subtract", StringComparison.OrdinalIgnoreCase))
            {
                return model.Subtract(tokens);
            }
            else if (tokens[0].Equals("multiplyrounded", StringComparison.OrdinalIgnoreCase))
            {
                return model.MultiplyRounded(tokens);
            }
            else if (tokens[0].Equals("sum", StringComparison.OrdinalIgnoreCase))
            {
                return model.Sum(tokens);
            }
            else if (tokens[0].Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                return model.Random(tokens);
            }
            else if (tokens[0].Equals("randomkey", StringComparison.OrdinalIgnoreCase))
            {
                return model.RandomKey(tokens);
            }
            else if (tokens[0].Equals("dictionary", StringComparison.OrdinalIgnoreCase))
            {
                return model.Dictionary(tokens);
            }
            else if (tokens[0].Equals("asnegative", StringComparison.OrdinalIgnoreCase))
            {
                return model.AsNegative(tokens);
            }
            else if (tokens[0].Equals("if", StringComparison.OrdinalIgnoreCase))
            {
                return model.If(tokens);
            }
            else if (tokens[0].Equals("isnull", StringComparison.OrdinalIgnoreCase))
            {
                return model.IsNull(tokens);
            }
            else if (tokens[0].Equals("isnotnull", StringComparison.OrdinalIgnoreCase))
            {
                return model.IsNotNull(tokens);
            }
            else if (tokens[0].Equals("istrue", StringComparison.OrdinalIgnoreCase))
            {
                return model.ToBool(tokens);
            }
            else if (tokens[0].Equals("isfalse", StringComparison.OrdinalIgnoreCase))
            {
                return !model.ToBool(tokens);
            }
            else if (tokens[0].Equals("lifetime", StringComparison.OrdinalIgnoreCase))
            {
                return model.GetLifetime(tokens);
            }
            else
            {
                throw new NotSupportedException("Not supported: " + tokens[0]);
            }
        }
    }

    public class NestedDirectiveModel
    {
        private static Random random = new Random();

        public object Random(string[] tokens)
        {
            //todo - find a way to support more than just strings here
            var variableName = TimeThread.Current.Resolve(tokens[1]).ToString();
            var collection = Heap.Current.Get<object>(variableName);

            if (collection is IList)
            {
                var list = collection as IList;
                return list[random.Next(0, list.Count)];
            }
            else if (collection is IDictionary)
            {
                var dictionary = collection as IDictionary;
                var list = new List<object>();
                foreach (var element in dictionary.Values) list.Add(element);
                return list[random.Next(0, list.Count)];
            }
            else
            {
                throw new NotSupportedException("Not supported");
            }
        }

        public object RandomKey(string[] tokens)
        {
            var variableName = TimeThread.Current.Resolve(tokens[1]).ToString();
            var collection = Heap.Current.Get<object>(tokens[1]);
            var dictionary = collection as Dictionary<string, ConsoleString>;
            var list = new List<object>();
            foreach (var element in dictionary.Keys) list.Add(element);
            return list[random.Next(0, list.Count)];
        }

        public object Dictionary(string[] tokens)
        {
            var variableName = TimeThread.Current.Resolve(tokens[1]).ToString();
            var key = TimeThread.Current.Resolve(tokens[2]).ToString();
            var collection = Heap.Current.Get<object>(variableName);
            var dictionary = collection as Dictionary<string, ConsoleString>;
            return dictionary[key];
        }

        public object Multiply(string[] tokens)
        {
            var ret = float.Parse(tokens[1]);
            for (var i = 2; i < tokens.Length; i++)
            {
                ret = ret * float.Parse(tokens[i]);
            }
            return ret;
        }

        public object Divide(string[] tokens)
        {
            var a = float.Parse(tokens[1]);
            var b = float.Parse(tokens[2]);
            var ret = a / b;
            return ret;
        }


        public object Subtract(string[] tokens)
        {
            var a = float.Parse(tokens[1]);
            var b = float.Parse(tokens[2]);
            var ret = a - b;
            return ret;
        }

        public object MultiplyRounded(string[] tokens)
        {
            var ret = float.Parse(tokens[1]);
            for (var i = 2; i < tokens.Length; i++)
            {
                ret = ret * float.Parse(tokens[i]);
            }
            return (float)ConsoleMath.Round(ret);
        }

        public object Sum(string[] tokens)
        {
            var ret = float.Parse(tokens[1]);
            for (var i = 2; i < tokens.Length; i++)
            {
                ret = ret + float.Parse(tokens[i]);
            }
            return ret;
        }

        public object AsNegative(string[] tokens)
        {
            var val = float.Parse(tokens[1]);
            if (val > 0)
            {
                return -val;
            }
            else
            {
                return val;
            }
        }

        public object If(string[] tokens)
        {
            var ifArgs = Args.Parse<IfArgs>(tokens.Skip(1).ToArray());
            return ifArgs.Eval();
        }

        public bool IsNull(string[] tokens)
        {
            if (tokens.Length > 2)
            {
                return false;
            }

            return tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]);
        }

        public bool IsNotNull(string[] tokens)
        {
            return !IsNull(tokens);
        }


        public bool ToBool(string[] tokens)
        {
            if (IsNull(tokens)) return false;
            else if (tokens.Length == 1) return false;

            return tokens[1].ToLower() == "true";
        }

        public ILifetime GetLifetime(string[] tokens)
        {
            return LifetimeDirective.Get(tokens[1]);
        }
    }

public class IfArgs
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

    public bool Eval() => Evaluator.Evaluate(Expression, Left, Operator, Right);
}
