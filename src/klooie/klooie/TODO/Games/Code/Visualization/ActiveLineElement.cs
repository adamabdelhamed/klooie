using PowerArgs;
namespace klooie.Gaming.Code;
public class ActiveLineElement : GameCollider
{
    public const ConsoleColor ActiveForegroundColor = ConsoleColor.Black;
    public const ConsoleColor ActiveBackgroundColor = ConsoleColor.Yellow;
    public const ConsoleColor ThrowBackgroundColor = ConsoleColor.Red;

    public const ConsoleColor AwaitForegroundColor = ConsoleColor.White;
    public const ConsoleColor AwaitBackgroundColor = ConsoleColor.DarkRed;

    private ConsoleString _lineOfCode;
    public ConsoleString LineOfCode
    {
        get => _lineOfCode; set
        {
            _lineOfCode = value;
            if (_lineOfCode.Length == 0) return;
            if (_lineOfCode[0].BackgroundColor == AwaitBackgroundColor)
            {
                ZIndex = 1;
            }
            else
            {
                ZIndex = 0;
            }
        }
    }
    public ActiveLineElement(IEnumerable<CodeToken> tokens, ILifetimeManager owner)
    {
        TransparentBackground = true;
        owner.OnDisposed(this.Dispose);
        this.LineOfCode = string.Join("", tokens.Select(t => t.Value)).Trim().ToConsoleString(ActiveForegroundColor, ActiveBackgroundColor);
        this.ResizeTo(this.LineOfCode.Length, 1);
        this.MoveTo(0, 0, 1);
    }

    public ActiveLineElement(IEnumerable<CodeToken> tokens, string throwMessage, ILifetimeManager owner)
    {
        owner.OnDisposed(this.Dispose);
        var underlyingCode = string.Join("", tokens.Select(t => t.Value)).Trim().ToConsoleString(ActiveForegroundColor, ActiveBackgroundColor);
        this.LineOfCode = throwMessage.ToConsoleString(ActiveForegroundColor, ThrowBackgroundColor);

        while (LineOfCode.Length < underlyingCode.Length)
        {
            LineOfCode += " ";
        }

        this.ResizeTo(underlyingCode.Length, 1);
        this.MoveTo(0, 0, 1);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        context.DrawString(LineOfCode, 0, 0);
    }
}

