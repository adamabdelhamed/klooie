namespace klooie.Gaming.Code;

/// <summary>
/// A type representing tokens parsed in klooie's code language
/// </summary>
public class CodeToken : Token
{
    /// <summary>
    /// Gets the statement this token is a part of
    /// </summary>
    public IStatement Statement { get; internal set; }

    /// <summary>
    /// Gets the type of this token
    /// </summary>
    public TokenType Type { get; internal set; }

    /// <summary>
    /// Gets the function that this token is a part of or null if it is
    /// not a part of a function
    /// </summary>
    public Function? Function { get; internal set; }

    internal CodeToken(int startIndex, int line, int col) : base(startIndex, line, col) { }

    /// <summary>
    /// tests to see if this token is located within a given statement
    /// </summary>
    /// <param name="other">the statement to test</param>
    /// <returns>true if this token is located within a given statement, false otherwise</returns>
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