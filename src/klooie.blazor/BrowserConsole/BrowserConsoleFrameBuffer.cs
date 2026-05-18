using System.Text;
using klooie;
using PowerArgs;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserConsoleFrameBuffer
{
    private BrowserConsoleCell[] cells;
    private readonly Dictionary<RGB, string> colorCache = new();

    public BrowserConsoleFrameBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        cells = new BrowserConsoleCell[width * height];
        Array.Fill(cells, BrowserConsoleCell.Empty);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public void CopyFrom(ConsoleBitmap source)
    {
        Resize(source.Width, source.Height);

        var pixels = source.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            cells[i] = new BrowserConsoleCell(pixel.Value, pixel.ForegroundColor, pixel.BackgroundColor);
        }
    }

    public BrowserConsoleFrame ToFrame()
    {
        var x = new List<int>(Height * 4);
        var y = new List<int>(Height * 4);
        var text = new List<string>(Height * 4);
        var foreground = new List<string>(Height * 4);
        var background = new List<string>(Height * 4);
        var builder = new StringBuilder(Width);

        for (var row = 0; row < Height; row++)
        {
            var rowOffset = row * Width;
            var runStart = 0;
            var current = cells[rowOffset];
            builder.Clear();

            for (var column = 0; column < Width; column++)
            {
                var cell = cells[rowOffset + column];
                if (column > 0 && !cell.HasSameStyle(current))
                {
                    AddRun(x, y, text, foreground, background, runStart, row, builder, current);
                    runStart = column;
                    builder.Clear();
                    current = cell;
                }

                builder.Append(char.IsWhiteSpace(cell.Glyph) ? '\u00A0' : cell.Glyph);
            }

            AddRun(x, y, text, foreground, background, runStart, row, builder, current);
        }

        return new BrowserConsoleFrame
        {
            Width = Width,
            Height = Height,
            X = x.ToArray(),
            Y = y.ToArray(),
            Text = text.ToArray(),
            Foreground = foreground.ToArray(),
            Background = background.ToArray()
        };
    }

    private void AddRun(
        List<int> x,
        List<int> y,
        List<string> text,
        List<string> foreground,
        List<string> background,
        int runStart,
        int row,
        StringBuilder builder,
        BrowserConsoleCell style)
    {
        x.Add(runStart);
        y.Add(row);
        text.Add(builder.ToString());
        foreground.Add(GetColor(style.ForegroundColor));
        background.Add(GetColor(style.BackgroundColor));
    }

    private void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (Width == width && Height == height) return;

        Width = width;
        Height = height;
        cells = new BrowserConsoleCell[width * height];
        Array.Fill(cells, BrowserConsoleCell.Empty);
    }

    private string GetColor(RGB color)
    {
        if (colorCache.TryGetValue(color, out var webColor)) return webColor;
        webColor = color.ToWebString();
        colorCache[color] = webColor;
        return webColor;
    }
}
