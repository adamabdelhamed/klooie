using PowerArgs;

namespace klooie.Gaming;

public class Cursor : GameCollider
{
    private static readonly ConsoleString DefaultStyle = new ConsoleString("X", ConsoleColor.DarkCyan, ConsoleColor.Cyan);
    public Cursor()
    {
        this.MoveTo(0, 0, int.MaxValue);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(DefaultStyle, 0, 0);
}

