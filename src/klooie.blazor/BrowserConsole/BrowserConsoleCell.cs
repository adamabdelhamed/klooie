namespace klooie.blazor.BrowserConsole;

public sealed record BrowserConsoleCell(string Glyph, string ForegroundColor, string BackgroundColor)
{
    public static BrowserConsoleCell Empty { get; } = new(" ", "#d7dde8", "#10141f");
}
