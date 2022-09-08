namespace klooie.Gaming;

public class Cursor : GameCollider
{
    private static readonly ConsoleString DefaultStyle = new ConsoleString("X", RGB.DarkCyan, RGB.Cyan);
    public Cursor()
    {
        this.MoveTo(0, 0, int.MaxValue);
    }

    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(DefaultStyle, 0, 0);
}

