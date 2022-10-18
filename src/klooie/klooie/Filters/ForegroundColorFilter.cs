namespace klooie;
public class ForegroundColorFilter : IConsoleControlFilter
{
    public RGB Color { get; set; }

    public ForegroundColorFilter(in RGB color)
    {
        this.Color = color;
    }

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }

    public void Filter(ConsoleBitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Value == ' ') continue;
                bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, Color, pixel.BackgroundColor));
            }
        }
    }
}