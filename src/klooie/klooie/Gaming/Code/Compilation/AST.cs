namespace klooie.Gaming.Code;
/// <summary>
/// An abstract syntax tree for klooie's code language
/// </summary>
public class AST : ICanBeAConsoleString, IRuleProvider
{
    /// <summary>
    /// The root block that all code lives within
    /// </summary>
    public Block Root { get; internal set; }

    /// <summary>
    /// Gets the tokens that represent the source code of this AST
    /// </summary>
    public List<CodeToken> Tokens { get; internal set; } = new List<CodeToken>();

    /// <summary>
    /// Gets the code representation of this AST
    /// </summary>
    /// <returns></returns>
    public override string ToString() => ToConsoleString().StringValue;

    /// <summary>
    /// Gets all functions defined within this AST
    /// </summary>
    public IEnumerable<Function> Functions => Root.Functions;

    /// <summary>
    /// Gets all rules defined by this AST
    /// </summary>
    public IRule[] Rules
    {
        get
        {
            var ret = new List<Directive>();
            Root.Visit((s) =>
            {
                if (s is Directive)
                {
                    ret.Add(s as Directive);
                }
                return false;
            });
            return ret.ToArray();

        }
    }

    /// <summary>
    /// Tries to get the function that the given token is a part of
    /// </summary>
    /// <param name="token">the token to test</param>
    /// <param name="ret">the function, if found</param>
    /// <returns>true if the token's function was found, false otherwise</returns>
    public bool TryGetFunction(CodeToken token, out Function ret) => TryGetFunction(token.Statement, out ret);

    /// <summary>
    /// Tries to get the function that the given statment is a part of
    /// </summary>
    /// <param name="s">the statment to test</param>
    /// <param name="ret">the function, if found</param>
    /// <returns>true if the token's function was found, false otherwise</returns>
    public bool TryGetFunction(IStatement s, out Function ret)
    {
        var current = s;

        while (current != null)
        {
            if (current.GetType() == typeof(Function))
            {
                ret = current as Function;
                return true;
            }
            else
            {
                current = current.Parent;
            }
        }

        ret = null;
        return false;
    }

    /// <summary>
    /// Gets a stylized version of the code
    /// </summary>
    /// <returns>a stylized version of the code</returns>
    public ConsoleString ToConsoleString()
    {
        var buffer = new List<ConsoleCharacter>();
        foreach (var token in Tokens)
        {
            if (token.Type == TokenType.Comment)
            {
                buffer.AddRange(token.Value.ToGreen());
            }
            else if (token.Type == TokenType.DoubleQuotedStringLiteral)
            {
                buffer.AddRange(token.Value.ToYellow());
            }
            else if (token.Type == TokenType.Keyword)
            {
                buffer.AddRange(token.Value.ToCyan());
            }
            else if (token.Type == TokenType.SpecialCharacter)
            {
                buffer.AddRange(token.Value.ToDarkGray());
            }
            else if (token.Type == TokenType.TrailingWhitespace)
            {
                buffer.AddRange(token.Value.ToConsoleString(bg: RGB.White));
            }
            else
            {
                buffer.AddRange(token.Value.ToWhite());
            }
        }

        return new ConsoleString(buffer);
    }
}
