using klooie;
using PowerArgs;

namespace klooie.blazor.BrowserConsole;

public readonly record struct BrowserConsoleCell(char Glyph, RGB ForegroundColor, RGB BackgroundColor)
{
    public static BrowserConsoleCell Empty { get; } = new(' ', ConsoleString.DefaultForegroundColor, ConsoleString.DefaultBackgroundColor);

    public bool HasSameStyle(BrowserConsoleCell other) => ForegroundColor == other.ForegroundColor && BackgroundColor == other.BackgroundColor;
}
