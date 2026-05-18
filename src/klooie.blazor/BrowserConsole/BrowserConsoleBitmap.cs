namespace klooie.blazor.BrowserConsole;

public sealed class BrowserConsoleBitmap
{
    private BrowserConsoleCell[] cells;

    public BrowserConsoleBitmap(int width, int height, BrowserConsoleCell? fill = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        cells = Enumerable.Repeat(fill ?? BrowserConsoleCell.Empty, width * height).ToArray();
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public IReadOnlyList<BrowserConsoleCell> Cells => cells;

    public BrowserConsoleCell this[int x, int y]
    {
        get => cells[GetIndex(x, y)];
        set => cells[GetIndex(x, y)] = value;
    }

    public void FillRect(int x, int y, int width, int height, BrowserConsoleCell cell)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                this[column, row] = cell;
            }
        }
    }

    public void Write(int x, int y, string text, string foregroundColor, string backgroundColor)
    {
        for (var i = 0; i < text.Length && x + i < Width; i++)
        {
            this[x + i, y] = new BrowserConsoleCell(text[i].ToString(), foregroundColor, backgroundColor);
        }
    }

    public void CopyFrom(klooie.ConsoleBitmap source)
    {
        Resize(source.Width, source.Height);

        var pixels = source.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            cells[i] = new BrowserConsoleCell(pixel.Value.ToString(), pixel.ForegroundColor.ToWebString(), pixel.BackgroundColor.ToWebString());
        }
    }

    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (Width == width && Height == height) return;

        Width = width;
        Height = height;
        cells = Enumerable.Repeat(BrowserConsoleCell.Empty, width * height).ToArray();
    }

    private int GetIndex(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)x, (uint)Width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)y, (uint)Height);
        return y * Width + x;
    }
}
