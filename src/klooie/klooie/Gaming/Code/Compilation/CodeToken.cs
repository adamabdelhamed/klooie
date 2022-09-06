using PowerArgs;

namespace klooie.Gaming.Code;
public class CodeToken : Token
{
    public IStatement Statement { get; set; }
    public TokenType Type { get; set; }
    public Function Function { get; set; }

    public CodeToken(int startIndex, int line, int col) : base(startIndex, line, col) { }

    public bool IsWithin(IStatement other)
    {
        var current = Statement;
        while (current != null)
        {
            if (current == other)
            {
                return true;
            }
            else
            {
                current = current.Parent;
            }
        }
        return false;
    }
}