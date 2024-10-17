namespace ScrollSucker;

public class StringCharacter : Character
{
    public ConsoleString Display { get; private set; }

    public StringCharacter(ConsoleString display)
    {
        this.Display = display;
        this.ResizeTo(Display.Length, 1);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        var toDraw = IsBeingTargeted ? Display.StringValue.ToBlack(bg: RGB.Cyan) : Display;
        context.DrawString(toDraw, 0, 0);
    }
}