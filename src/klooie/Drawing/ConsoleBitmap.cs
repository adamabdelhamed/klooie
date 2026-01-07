using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace klooie;
/// <summary>
/// A data structure representing a 2d image that can be pained in
/// a console window
/// </summary>
public sealed class ConsoleBitmap : Recyclable
{
    private static readonly ConsoleCharacter EmptySpace = new ConsoleCharacter(' ');

    // todo: make internal after migration
    /// <summary>
    /// Don't use. Will be made internal.
    /// </summary>
    [ThreadStatic]
    public static Loc[] LineBuffer;

    // larger is faster, but may cause gaps
    private const float DrawPrecision = .5f;

    /// <summary>
    /// The width of the image, in number of character pixels
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// The height of the image, in number of character pixels
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// The console to target when the Paint method is called 
    /// </summary>
    public IConsoleProvider Console { get; set; }

    /// <summary>
    /// Gets raw access to the pixels. May improve performance, but is more dangerous than
    /// using the built in methods. If you modify the values to an inconsistent state then
    /// you can break the object.
    /// </summary>
    internal ConsoleCharacter[] Pixels; // flattened [y * Width + x]


    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal int IndexOf(int x, int y) => y * Width + x;

    /// <summary>
    /// Creates a new ConsoleBitmap
    /// </summary>
    /// <param name="w">the width of the image</param>
    /// <param name="h">the height of the image</param>
    private ConsoleBitmap() { }
    public static ConsoleBitmap Create(int w, int h)
    {
        var ret = pool.Value.Rent();
        ret.Width = w;
        ret.Height = h;
        ret.Console = ConsoleProvider.Current;
        ret.Pixels = ArrayPool<ConsoleCharacter>.Shared.Rent(w * h);
        // Fill with EmptySpace
        var span = ret.Pixels.AsSpan(0, w * h);
        for (int i = 0; i < span.Length; i++) span[i] = EmptySpace;
        return ret;
    }

    private static LazyPool<ConsoleBitmap> pool = new LazyPool<ConsoleBitmap>(() => new ConsoleBitmap());

    protected override void OnReturn()
    {
        base.OnReturn();
        ArrayPool<ConsoleCharacter>.Shared.Return(Pixels);
        Pixels = Array.Empty<ConsoleCharacter>();
        Width = 0;
        Height = 0;
    }

    /// <summary>
    /// Coonverts this ConsoleBitmap to the Console Video Format
    /// </summary>
    /// <param name="outputStream">The sream to write the video to</param>
    public void ToSingleFrameVideo(Stream outputStream)
    {
        var bitmapVideoWriter = new ConsoleBitmapVideoWriter(s => outputStream.Write(Encoding.Default.GetBytes(s)));
        bitmapVideoWriter.WriteFrame(this).Clone();
        bitmapVideoWriter.Finish();
    }

    /// <summary>
    /// Converts this ConsoleBitmap to a ConsoleString
    /// </summary>
    /// <param name="trimMode">if false (the default), unformatted whitespace at the end of each line will be included as whitespace in the return value. If true, that whitespace will be trimmed from the return value.</param>
    /// <returns>the bitmap as a ConsoleString</returns>
    public ConsoleString ToConsoleString(bool trimMode = false)
    {
        List<ConsoleCharacter> chars = new List<ConsoleCharacter>();
        for (var y = 0; y < this.Height; y++)
        {
            for (var x = 0; x < this.Width; x++)
            {
                if (trimMode && IsRestOfLineWhitespaceWithDefaultBackground(x, y))
                {
                    break;
                }
                else
                {
                    var pixel = this.GetPixel(x, y);
                    chars.Add(pixel);
                }
            }
            if (y < this.Height - 1)
            {
                chars.Add(new ConsoleCharacter('\n'));
            }
        }

        return new ConsoleString(chars);
    }

    private bool IsRestOfLineWhitespaceWithDefaultBackground(int xStart, int y)
    {
        var defaultBg = new ConsoleCharacter(' ').BackgroundColor;

        for (var x = xStart; x < this.Width; x++)
        {
            if (char.IsWhiteSpace(this.GetPixel(x, y).Value) && this.GetPixel(x, y).BackgroundColor == defaultBg)
            {
                // this is whitespace
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resizes this image, preserving the data in the pixels that remain in the new area
    /// </summary>
    /// <param name="w">the new width</param>
    /// <param name="h">the new height</param>
    public void Resize(int w, int h)
    {
        if (w == Width && h == Height) return;

        var newPixels = ArrayPool<ConsoleCharacter>.Shared.Rent(w * h);

        // copy overlap
        int minW = Math.Min(w, Width);
        int minH = Math.Min(h, Height);
        for (int yy = 0; yy < minH; yy++)
        {
            int srcRow = yy * Width;
            int dstRow = yy * w;
            for (int xx = 0; xx < minW; xx++)
            {
                newPixels[dstRow + xx] = Pixels[srcRow + xx];
            }
        }
        // fill the rest with EmptySpace
        for (int i = 0; i < newPixels.Length; i++)
        {
            if (i >= (minH * w) || (i % w) >= minW) newPixels[i] = EmptySpace;
        }

        ArrayPool<ConsoleCharacter>.Shared.Return(Pixels);

        Pixels = newPixels;
        Width = w;
        Height = h;
    }

    /// <summary>
    /// Gets the pixel at the given location
    /// </summary>
    public ref ConsoleCharacter GetPixel(int x, int y) => ref Pixels[IndexOf(x, y)];

    public ref ConsoleCharacter GetPixel(float x, float y)
    {
        return ref GetPixel(ConsoleMath.Round(x), ConsoleMath.Round(y));
    }

    /// <summary>
    /// Sets the value of the desired pixel
    /// </summary>
    public void SetPixel(int x, int y, in ConsoleCharacter c) => Pixels[IndexOf(x, y)] = c;

    public void SetPixel(float x, float y, in ConsoleCharacter c)
    {
        SetPixel(ConsoleMath.Round(x), ConsoleMath.Round(y), c);
    }

    /// <summary>
    /// tests to see if the given coordinates are within the boundaries
    /// of the image
    /// </summary>
    public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Draws the given string onto the bitmap
    /// </summary>
    public void DrawString(string str, int x, int y, bool vert = false) => DrawString(new ConsoleString(str), x, y, vert);

    /// <summary>
    /// Draws a filled in rectangle bounded by the given coordinates
    /// using the current pen
    /// </summary>
    public void FillRect(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        var maxX = Math.Min(x + w, Width);
        var maxY = Math.Min(y + h, Height);

        var minX = Math.Max(x, 0);
        var minY = Math.Max(y, 0);

        for (int yy = minY; yy < maxY; yy++)
        {
            int row = yy * Width;
            for (int xx = minX; xx < maxX; xx++)
            {
                Pixels[row + xx] = pen;
            }
        }
    }

    public void FillRectChecked(in RGB color, in Rect rect) => FillRectChecked(new ConsoleCharacter(' ', backgroundColor: color), rect.Left, rect.Top, rect.Width, rect.Height);
    public void FillRectChecked(in ConsoleCharacter pen, in Rect rect) => FillRectChecked(pen, rect.Left, rect.Top, rect.Width, rect.Height);
    public void FillRectChecked(in RGB color, int x, int y, int w, int h) => FillRectChecked(new ConsoleCharacter(' ', backgroundColor: color), x, y, w, h);

    public void FillRectChecked(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;

        int minX = Math.Max(0, x);
        int minY = Math.Max(0, y);
        int maxX = Math.Min(Width, x + w);
        int maxY = Math.Min(Height, y + h);

        if (minX >= maxX || minY >= maxY) return;

        for (int yy = minY; yy < maxY; yy++)
        {
            int row = yy * Width;
            for (int xx = minX; xx < maxX; xx++)
            {
                Pixels[row + xx] = pen;
            }
        }
    }

    /// <summary>
    /// Fills the given rectangle with a space character and a given background color
    /// </summary>
    public void FillRect(in RGB color, int x, int y, int w, int h) => FillRect(new ConsoleCharacter(' ', backgroundColor: color), x, y, w, h);

    /// <summary>
    /// Fills the given rectangle with a space character and a given background color
    /// </summary>
    public void FillRect(in RGB color, in Rect rect) => FillRect(color, rect.Left, rect.Top, rect.Width, rect.Height);
    /// <summary>
    /// Fills the entire bitmap with a space character and a given background color
    /// </summary>
    public void Fill(in RGB color) => Fill(new ConsoleCharacter(' ', backgroundColor: color));

    /// <summary>
    /// Fills the entire bitmap with a given pen
    /// </summary>
    public void Fill(in ConsoleCharacter pen)
    {
        var span = Pixels.AsSpan(0, Width * Height);
        for (int i = 0; i < span.Length; i++) span[i] = pen;
    }

    /// <summary>
    /// Draws a filled in rectangle bounded by the given coordinates
    /// using the current pen, without performing bounds checks
    /// </summary>
    public void FillRectUnsafe(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        var maxX = x + w;
        var maxY = y + h;

        for (int yy = y; yy < maxY; yy++)
        {
            int row = yy * Width;
            for (int xx = x; xx < maxX; xx++)
            {
                Pixels[row + xx] = pen;
            }
        }
    }

    public void DrawRect(in RGB pen, int x, int y, int w, int h)
        => DrawRect(new ConsoleCharacter(' ', RGB.Black, pen), x, y, w, h);

    /// <summary>
    /// Draws an unfilled in rectangle bounded by the given coordinates
    /// using the current pen
    /// </summary>
    public void DrawRect(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        var maxX = Math.Min(x + w, Width);
        var maxY = Math.Min(y + h, Height);
        var minX = Math.Max(x, 0);
        var minY = Math.Max(y, 0);

        if (minX >= maxX || minY >= maxY) return;

        var xEndIndex = maxX - 1;
        var yEndIndex = maxY - 1;

        // left vertical line
        for (var yd = minY; yd < maxY; yd++)
        {
            Pixels[yd * Width + minX] = pen;
        }

        // right vertical line
        for (var yd = minY; yd < maxY; yd++)
        {
            Pixels[yd * Width + xEndIndex] = pen;
        }

        // top horizontal line
        int topRow = minY * Width;
        for (int xd = minX; xd < maxX; xd++)
        {
            Pixels[topRow + xd] = pen;
        }

        // bottom horizontal line
        int bottomRow = yEndIndex * Width;
        for (int xd = minX; xd < maxX; xd++)
        {
            Pixels[bottomRow + xd] = pen;
        }
    }

    /// <summary>
    /// Draws the given string onto the bitmap
    /// </summary>
    public void DrawString(ConsoleString str, int x, int y, bool vert = false)
    {
        var xStart = x;
        var span = str.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var character = span[i];
            if (character.Value == '\n')
            {
                y++;
                x = xStart;
                continue;
            }
            else if (character.Value == '\r')
            {
                // ignore
            }
            else if (IsInBounds(x, y))
            {
                Pixels[IndexOf(x, y)] = character;
            }

            if (vert) y++;
            else x++;
        }
    }

    public void DrawString(ConsoleString str, RGB fg, RGB bg, int x, int y, bool vert = false)
    {
        var xStart = x;
        var span = str.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var character = span[i];
            if (character.Value == '\n')
            {
                y++;
                x = xStart;
                continue;
            }
            else if (character.Value == '\r')
            {
                // ignore
            }
            else if (IsInBounds(x, y))
            {
                Pixels[IndexOf(x, y)] = new ConsoleCharacter(character.Value, fg, bg);
            }

            if (vert) y++;
            else x++;
        }
    }

    public void DrawString(string str, RGB fg, RGB bg, int x, int y, bool vert = false)
    {
        var xStart = x;
        var span = str.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var character = span[i];
            if (character == '\n')
            {
                y++;
                x = xStart;
                continue;
            }
            else if (character == '\r')
            {
                // ignore
            }
            else if (IsInBounds(x, y))
            {
                Pixels[IndexOf(x, y)] = new ConsoleCharacter(character, fg, bg);
            }

            if (vert) y++;
            else x++;
        }
    }

    public void DrawString(ConsoleCharacter[] buffer, int x, int y, bool vert = false)
    {
        var xStart = x;
        for (var i = 0; i < buffer.Length; i++)
        {
            var character = buffer[i];
            if (character.Value == '\n')
            {
                y++;
                x = xStart;
                continue;
            }
            else if (character.Value == '\r')
            {
                // ignore
            }
            else if (IsInBounds(x, y))
            {
                Pixels[IndexOf(x, y)] = character;
            }

            if (vert) y++;
            else x++;
        }
    }

    /// <summary>
    /// Draw a single pixel value at the given point using the current pen
    /// </summary>
    public void DrawPoint(in ConsoleCharacter pen, int x, int y)
    {
        if (IsInBounds(x, y))
        {
            Pixels[IndexOf(x, y)] = pen;
        }
    }

    public void DrawPoint(in RGB pen, int x, int y)
        => DrawPoint(new ConsoleCharacter(' ', RGB.Black, pen), x, y);

    public void DrawLine(in RGB pen, int x1, int y1, int x2, int y2)
        => DrawLine(new ConsoleCharacter(' ', RGB.Black, pen), x1, y1, x2, y2);

    /// <summary>
    /// Draw a line segment between the given points
    /// </summary>
    public void DrawLine(in ConsoleCharacter pen, int x1, int y1, int x2, int y2)
    {
        var len = DefineLineBuffered(x1, y1, x2, y2);
        Loc point;
        for (var i = 0; i < len; i++)
        {
            point = LineBuffer[i];
            if (IsInBounds(point.Left, point.Top))
            {
                Pixels[IndexOf(point.Left, point.Top)] = pen;
            }
        }
    }

    /// <summary>
    /// Draws the given bitmap onto this bitmap
    /// </summary>
    public void DrawBitmap(ConsoleBitmap bitmap, int offsetX, int offsetY)
    {
        for (var x = 0; x < bitmap.Width && x < Width; x++)
        {
            for (var y = 0; y < bitmap.Height && y < Height; y++)
            {
                var bmpX = x + offsetX;
                var bmpY = y + offsetY;
                if (bmpX < 0 || bmpX >= bitmap.Width || bmpY < 0 || bmpY >= bitmap.Height) continue;
                ref var pixel = ref bitmap.GetPixel(x + offsetX, y + offsetY);
                DrawPoint(pixel, x, y);
            }
        }
    }

    /// <summary>
    /// Given 2 points, defines a line as it can best be rendered in a ConsoleBitmap, but does not draw the line.
    /// </summary>
    public static int DefineLineBuffered(int x1, int y1, int x2, int y2, Loc[] buffer = null)
    {
        // Use provided buffer, or use a static one, allocating if needed
        if (buffer == null)
        {
            LineBuffer ??= new Loc[10000];
            buffer = LineBuffer;
        }

        int ret = 0;

        // Bresenham's Line Algorithm
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);

        int sx = (x1 < x2) ? 1 : -1;
        int sy = (y1 < y2) ? 1 : -1;

        int x = x1;
        int y = y1;

        bool steep = dy > dx;
        int err = (steep ? dy : dx) / 2;

        int n = (steep ? dy : dx) + 1; // Number of points (inclusive)
        for (int i = 0; i < n; i++)
        {
            buffer[ret++] = new Loc(x, y);

            if (x == x2 && y == y2)
                break;

            if (steep)
            {
                y += sy;
                err -= dx;
                if (err < 0)
                {
                    x += sx;
                    err += dy;
                }
            }
            else
            {
                x += sx;
                err -= dy;
                if (err < 0)
                {
                    y += sy;
                    err += dx;
                }
            }
        }

        return ret;
    }

    /// <summary>
    /// Makes a copy of this bitmap
    /// </summary>
    public ConsoleBitmap Clone()
    {
        var clone = ConsoleBitmap.Create(Width, Height);
        var src = Pixels.AsSpan(0, Width * Height);
        var dst = clone.Pixels.AsSpan(0, Width * Height);
        src.CopyTo(dst);
        return clone;
    }


    /// <summary>
    /// Gets a string representation of this image 
    /// </summary>
    public override string ToString() => ToConsoleString().ToString();

    /// <summary>
    /// Returns true if the given object is a ConsoleBitmap with
    /// equivalent values as this bitmap, false otherwise
    /// </summary>
    public override bool Equals(Object obj)
    {
        var other = obj as ConsoleBitmap;
        if (other == null) return false;

        if (this.Width != other.Width || this.Height != other.Height)
        {
            return false;
        }

        for (var x = 0; x < this.Width; x++)
        {
            for (var y = 0; y < this.Height; y++)
            {
                var thisVal = this.GetPixel(x, y);
                var otherVal = other.GetPixel(x, y);
                if (thisVal.Equals(otherVal) == false) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a hashcode for this bitmap
    /// </summary>
    public override int GetHashCode() => base.GetHashCode();

    /// <summary>
    /// Creates a visual diff of two bitmaps. The returned bitmap will have the same dimensions as the input bitmaps.
    /// </summary>
    public static ConsoleBitmap? Diff(ConsoleBitmap a, ConsoleBitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return null;
        }

        var ret = ConsoleBitmap.Create(a.Width, a.Height);
        for (var x = 0; x < a.Width; x++)
        {
            for (var y = 0; y < a.Height; y++)
            {
                var pixelA = a.GetPixel(x, y);
                var pixelB = b.GetPixel(x, y);
                if (pixelA.Equals(pixelB) == false)
                {
                    ret.SetPixel(x, y, new ConsoleCharacter(pixelB.Value, RGB.Red, RGB.White));
                }
                else
                {
                    ret.SetPixel(x, y, new ConsoleCharacter(pixelA.Value, new RGB(50, 50, 50), RGB.Black));
                }
            }
        }
        return ret;
    }
}


public static class ConsoleStringEx
{
    public static ConsoleBitmap ToConsoleBitmap(this ConsoleString cstring)
    {
        var str = cstring.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\t", "    ");
        var lines = str.Split("\n");
        var h = lines.Count;
        var w = lines.Select(l => l.Length).Max();
        var ret = ConsoleBitmap.Create(w, h);

        var x = 0;
        var y = 0;

        foreach (var c in str)
        {
            if (c.Value == '\n')
            {
                x = 0;
                y++;
            }
            else
            {
                ret.GetPixel(x++, y) = c;
            }
        }

        return ret;
    }
}
