namespace klooie.Gaming.Code;


public class CodeControl : GameCollider
{
    public CodeToken Token;

    public CodeControl(CodeToken token)
    {
        CompositionMode = CompositionMode.BlendBackground;
        this.MoveTo(this.Left, this.Top);
        this.ResizeTo(token.Value.Length, 1);
        this.Token = token;
    }

    public virtual ConsoleString LineOfCode
    {
        get
        {
            return Token.Value.ToConsoleString();
        }
    }

    public override string ToString() => $"{Token.ToString()}";

    public ConsoleString FormatToken()
    {
        var token = Token;

        RGB fg = RGB.Gray, bg = RGB.Black;


        if (token.Statement is Directive)
        {
            bg = RGB.DarkGreen;
        }

        if (token.Type == TokenType.Comment)
        {
            fg = RGB.Green;
        }
        else if (token.Type == TokenType.Keyword)
        {
            fg = RGB.Cyan;
        }
        else if (token.Type == TokenType.TypeName)
        {
            fg = RGB.Magenta;
        }
        else if (token.Type == TokenType.DoubleQuotedStringLiteral)
        {
            return DoubleQuotedStringFormat();
            fg = RGB.DarkYellow;
        }
        else if (token.Type == TokenType.TrailingWhitespace)
        {
            bg = RGB.White;
        }
        else if (token.Type == TokenType.NonTrailingWhitespace)
        {
            bg = RGB.White;
        }

        return token.Value.ToConsoleString(fg, bg);
    }

    private ConsoleString DoubleQuotedStringFormat()
    {
        if (Token.Value == "[")
        {
            return new ConsoleString("[", RGB.DarkGray);
        }
        else if (Token.Value == "]")
        {
            return new ConsoleString("]", RGB.DarkGray);
        }
        if (Token.Value == "{")
        {
            return new ConsoleString("{", RGB.DarkGray);
        }
        else if (Token.Value == "}")
        {
            return new ConsoleString("}", RGB.DarkGray);
        }
        else if (RGB.TryParse(Token.Value, out RGB color))
        {
            return new ConsoleString(Token.Value, color);
        }
        else if (Heap.Current.TryGetValue(Token.Value, out object val))
        {
            return new ConsoleString(Token.Value, RGB.Gray);
        }
        else
        {
            return new ConsoleString(Token.Value, RGB.DarkYellow);
        }
    }

    public static IEnumerable<CodeControl> CodeElements => Game.Current.GamePanel.Controls.WhereAs<CodeControl>();

    public static IEnumerable<CodeControl> CompiledCodeElements => CodeElements
        .ToArray();

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(FormatToken(), 0, 0);
}

