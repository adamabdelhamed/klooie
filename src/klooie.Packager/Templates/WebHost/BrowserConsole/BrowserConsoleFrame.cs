using klooie;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserConsoleFrame
{
    public static BrowserConsoleFrame Empty { get; } = new()
    {
        Width = 1,
        Height = 1,
        Full = true,
        X = Array.Empty<int>(),
        Y = Array.Empty<int>(),
        Text = Array.Empty<string>(),
        Foreground = Array.Empty<int>(),
        Background = Array.Empty<int>(),
        TouchButtonReleases = Array.Empty<int>(),
        TouchButtonHints = Array.Empty<BrowserTouchButtonHint>()
    };

    public required int Width { get; init; }
    public required int Height { get; init; }
    public required bool Full { get; init; }
    public required int[] X { get; init; }
    public required int[] Y { get; init; }
    public required string[] Text { get; init; }
    public required int[] Foreground { get; init; }
    public required int[] Background { get; init; }
    public int[] TouchButtonReleases { get; init; } = Array.Empty<int>();
    public BrowserTouchButtonHint[] TouchButtonHints { get; init; } = Array.Empty<BrowserTouchButtonHint>();
}
