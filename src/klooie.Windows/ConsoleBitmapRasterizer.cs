using PowerArgs;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.CompilerServices;

namespace klooie;

public sealed class ConsoleBitmapRasterizer : IDisposable
{
    private readonly ConsoleRendererScaleProfile scaleProfile;
    private readonly ConsoleGlyphAtlas glyphAtlas;
    private readonly Dictionary<(char c, RGB color), Bitmap> tintedGlyphCache = new();
    private readonly Dictionary<RGB, SolidBrush> backgroundBrushCache = new();

    public ConsoleBitmapRasterizer(ConsoleRendererScaleProfile scaleProfile)
    {
        this.scaleProfile = scaleProfile ?? throw new ArgumentNullException(nameof(scaleProfile));
        glyphAtlas = new ConsoleGlyphAtlas(scaleProfile);
    }

    public void Rasterize(ConsoleBitmap bitmap, Bitmap frameBuffer)
    {
        if (frameBuffer == null) throw new ArgumentNullException(nameof(frameBuffer));

        using var graphics = Graphics.FromImage(frameBuffer);
        Rasterize(bitmap, frameBuffer, graphics);
    }

    public void Rasterize(ConsoleBitmap bitmap, Bitmap frameBuffer, Graphics graphics)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
        if (frameBuffer == null) throw new ArgumentNullException(nameof(frameBuffer));
        if (graphics == null) throw new ArgumentNullException(nameof(graphics));

        if (frameBuffer.Width < bitmap.Width * scaleProfile.CellPixelWidth ||
            frameBuffer.Height < bitmap.Height * scaleProfile.CellPixelHeight)
        {
            throw new ArgumentException("The frame buffer is too small for the bitmap and scale profile.", nameof(frameBuffer));
        }

        ConfigureGraphics(graphics);
        graphics.Clear(Color.Black);
        DrawBackgrounds(bitmap, graphics);
        DrawForegroundGlyphs(bitmap, graphics);
    }

    private void ConfigureGraphics(Graphics graphics)
    {
        graphics.TextRenderingHint = scaleProfile.TextRenderingHint;
        graphics.PixelOffsetMode = scaleProfile.PixelOffsetMode;
        graphics.SmoothingMode = scaleProfile.SmoothingMode;
        graphics.InterpolationMode = scaleProfile.InterpolationMode;
        graphics.CompositingMode = scaleProfile.CompositingMode;
        graphics.CompositingQuality = scaleProfile.CompositingQuality;
        graphics.TextContrast = scaleProfile.TextContrast;
    }

    private SolidBrush GetBackgroundBrush(RGB color)
    {
        if (backgroundBrushCache.TryGetValue(color, out var brush) == false)
        {
            brush = new SolidBrush(Color.FromArgb(color.R, color.G, color.B));
            backgroundBrushCache[color] = brush;
        }

        return brush;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrawBackgrounds(ConsoleBitmap bitmap, Graphics graphics)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var top = y * scaleProfile.CellPixelHeight;
            var runStart = 0;
            var runColor = bitmap.GetPixel(0, y).BackgroundColor;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var isLastCell = x == bitmap.Width - 1;
                var nextColorDifferent = isLastCell == false && bitmap.GetPixel(x + 1, y).BackgroundColor.Equals(runColor) == false;

                if (isLastCell || nextColorDifferent)
                {
                    var bg = GetBackgroundBrush(runColor);
                    graphics.FillRectangle(bg, runStart * scaleProfile.CellPixelWidth, top, (x - runStart + 1) * scaleProfile.CellPixelWidth, scaleProfile.CellPixelHeight);

                    if (isLastCell == false)
                    {
                        runStart = x + 1;
                        runColor = bitmap.GetPixel(runStart, y).BackgroundColor;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrawForegroundGlyphs(ConsoleBitmap bitmap, Graphics graphics)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var top = y * scaleProfile.CellPixelHeight;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var cell = bitmap.GetPixel(x, y);
                if (cell.Value == ' ') continue;

                var tinted = GetTintedGlyph(cell.Value, cell.ForegroundColor);
                var left = x * scaleProfile.CellPixelWidth;
                graphics.DrawImageUnscaled(tinted, left - ConsoleGlyphAtlas.GlyphPaddingX, top);
            }
        }
    }

    private Bitmap GetTintedGlyph(char c, RGB color)
    {
        if (tintedGlyphCache.TryGetValue((c, color), out var tinted)) return tinted;

        var glyph = glyphAtlas.GetGlyph(c);
        tinted = TintGlyph(glyph, color);
        tintedGlyphCache[(c, color)] = tinted;
        return tinted;
    }

    private static Bitmap TintGlyph(Bitmap source, RGB color)
    {
        var tinted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(tinted);
        using var attributes = new ImageAttributes();

        var r = color.R / 255f;
        var gValue = color.G / 255f;
        var b = color.B / 255f;

        var matrix = new ColorMatrix(new[]
        {
            new[] { 0f, 0f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { r,  gValue, b,  0f, 1f },
        });

        attributes.SetColorMatrix(matrix);

        g.DrawImage(
            source,
            new Rectangle(0, 0, source.Width, source.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);

        return tinted;
    }

    public void Dispose()
    {
        foreach (var brush in backgroundBrushCache.Values)
        {
            brush.Dispose();
        }

        foreach (var glyph in tintedGlyphCache.Values)
        {
            glyph.Dispose();
        }

        backgroundBrushCache.Clear();
        tintedGlyphCache.Clear();
        glyphAtlas.Dispose();
    }
}

public sealed class ConsoleGlyphAtlas : IDisposable
{
    private readonly Dictionary<char, Bitmap> glyphs = new();
    private readonly Font font;
    private readonly StringFormat glyphFormat;
    private readonly ConsoleRendererScaleProfile scaleProfile;

    public const int GlyphPaddingX = 4;
    public const int GlyphPaddingY = 8;

    public ConsoleGlyphAtlas(ConsoleRendererScaleProfile scaleProfile)
    {
        this.scaleProfile = scaleProfile ?? throw new ArgumentNullException(nameof(scaleProfile));
        font = new Font(scaleProfile.FontFamilyName, scaleProfile.FontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel);

        glyphFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        glyphFormat.Alignment = StringAlignment.Near;
        glyphFormat.LineAlignment = StringAlignment.Near;
        glyphFormat.FormatFlags |= StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces;

        BuildBootstrapGlyphs();
    }

    public int GlyphBitmapWidth => scaleProfile.CellPixelWidth + (GlyphPaddingX * 2);
    public int GlyphBitmapHeight => scaleProfile.CellPixelHeight + (GlyphPaddingY * 2);
    public int CellPixelWidth => scaleProfile.CellPixelWidth;
    public int CellPixelHeight => scaleProfile.CellPixelHeight;

    public Bitmap GetGlyph(char c)
    {
        if (glyphs.TryGetValue(c, out var glyph)) return glyph;

        glyph = TryRenderGlyph(c);
        glyphs[c] = glyph;
        return glyph;
    }

    private Bitmap TryRenderGlyph(char c)
    {
        try
        {
            var glyph = RenderGlyph(c);
            if (GlyphHasVisiblePixels(glyph)) return glyph;

            glyph.Dispose();
            return glyphs['?'];
        }
        catch
        {
            return glyphs['?'];
        }
    }

    private static bool GlyphHasVisiblePixels(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A != 0) return true;
            }
        }

        return false;
    }

    private void BuildBootstrapGlyphs()
    {
        glyphs['?'] = RenderGlyph('?');
        glyphs[' '] = RenderGlyph(' ');
    }

    private Bitmap RenderGlyph(char c)
    {
        var bitmap = new Bitmap(GlyphBitmapWidth, GlyphBitmapHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.White);

        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = scaleProfile.TextRenderingHint;
        graphics.PixelOffsetMode = scaleProfile.PixelOffsetMode;
        graphics.SmoothingMode = scaleProfile.SmoothingMode;
        graphics.InterpolationMode = scaleProfile.InterpolationMode;
        graphics.CompositingMode = scaleProfile.CompositingMode;
        graphics.CompositingQuality = scaleProfile.CompositingQuality;
        graphics.TextContrast = scaleProfile.TextContrast;

        var oldTransform = graphics.Transform;
        try
        {
            graphics.TranslateTransform(GlyphPaddingX + scaleProfile.TextOffsetX, GlyphPaddingY + scaleProfile.TextOffsetY);
            graphics.ScaleTransform(scaleProfile.TextScaleX, scaleProfile.TextScaleY);
            graphics.DrawString(c.ToString(), font, brush, new PointF(0, 0), glyphFormat);
        }
        finally
        {
            graphics.Transform = oldTransform;
        }

        return bitmap;
    }

    public void Dispose()
    {
        foreach (var glyph in glyphs.Values)
        {
            glyph.Dispose();
        }

        glyphs.Clear();
        glyphFormat.Dispose();
        font.Dispose();
    }
}
