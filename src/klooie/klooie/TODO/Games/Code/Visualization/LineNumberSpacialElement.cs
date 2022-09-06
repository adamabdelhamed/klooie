using PowerArgs;
namespace klooie.Gaming.Code;
public class LineNumberControl : GameCollider
{
    public CodeDisplayState State { get; set; } = CodeDisplayState.Normal;

    public int Line { get; private set; }

    public LineNumberControl(int line)
    {
        this.MoveTo(Left, Top);
        this.Line = line;
        this.ResizeTo(line.ToString().Length, 1);
    }

    public override string ToString() => $"Line {Line}";

    protected override void OnPaint(ConsoleBitmap context)
    {
        var color = State == CodeDisplayState.Normal ? ConsoleColor.Gray
            : State == CodeDisplayState.Infected ? ConsoleColor.Red
            : ConsoleColor.Black;

        var bg = State == CodeDisplayState.InfectedWithHotfixReady ? ConsoleColor.White : ConsoleColor.Black;
        context.DrawString(Line.ToString().ToConsoleString(color, bg), 0, 0);
    }
}
 