using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using VdTexture = Veldrid.Texture;
using VdTextureView = Veldrid.TextureView;

namespace klooie;

public sealed class GlyphAtlasTex : IDisposable
{
    public const int Cols = 16;
    public const int Rows = 16;

    public readonly int CellW;
    public readonly int CellH;

    public readonly int AtlasW;
    public readonly int AtlasH;

    public readonly VdTexture Tex;
    public readonly VdTextureView View;


    private static readonly ConditionalWeakTable<Veldrid.GraphicsDevice, Dictionary<AtlasKey, CachedAtlas>> cacheByDevice = new();

    private readonly record struct AtlasKey(int CellW, int Supersample);

    private sealed class CachedAtlas
    {
        public required int CellW { get; init; }
        public required int CellH { get; init; }
        public required int AtlasW { get; init; }
        public required int AtlasH { get; init; }
        public required VdTexture Tex { get; init; }
        public required VdTextureView View { get; init; }
    }

    public GlyphAtlasTex(Veldrid.GraphicsDevice gd, Veldrid.ResourceFactory factory, int cellW, int supersample)
    {
        supersample = Math.Max(1, supersample);
        cellW = Math.Max(4, cellW);

        var key = new AtlasKey(cellW, supersample);
        var perDevice = cacheByDevice.GetOrCreateValue(gd);

        lock (perDevice)
        {
            if (!perDevice.TryGetValue(key, out var cached))
            {
                var atlasCellW = cellW * supersample;
                var atlasCellH = atlasCellW * 2;
                var atlasW = atlasCellW * Cols;
                var atlasH = atlasCellH * Rows;

                var tex = factory.CreateTexture(Veldrid.TextureDescription.Texture2D(
                    (uint)atlasW, (uint)atlasH, 1, 1,
                    Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,
                    Veldrid.TextureUsage.Sampled));

                var view = factory.CreateTextureView(tex);

                var rgba = BuildAtlasRgba(atlasCellW, atlasCellH, atlasW, atlasH);
                gd.UpdateTexture(tex, rgba, 0, 0, 0, (uint)atlasW, (uint)atlasH, 1, 0, 0);

                cached = new CachedAtlas
                {
                    CellW = atlasCellW,
                    CellH = atlasCellH,
                    AtlasW = atlasW,
                    AtlasH = atlasH,
                    Tex = tex,
                    View = view,
                };
                perDevice[key] = cached;
            }

            CellW = cached.CellW;
            CellH = cached.CellH;
            AtlasW = cached.AtlasW;
            AtlasH = cached.AtlasH;
            Tex = cached.Tex;
            View = cached.View;
        }
    }

    private static byte[] BuildAtlasRgba(int cellW, int cellH, int atlasW, int atlasH)
    {
        using var bmp = new Bitmap(atlasW, atlasH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        using var format = new StringFormat(StringFormatFlags.NoClip)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoWrap
        };

        var fontPx = (int)(cellH * 0.75f);
        using var font = new Font("Consolas", fontPx, FontStyle.Regular, GraphicsUnit.Pixel);


        using var brush = new SolidBrush(Color.White);
        var padX = Math.Max(1, cellW / 16);
        var padTop = Math.Max(1, cellH / 24);
        var padBottom = Math.Max(4, cellH / 12);


        for (var i = 0; i < 256; i++)
        {
            var ch = (char)i;

            // Skip controls for now
            if (char.IsControl(ch)) continue;

            var gx = i % Cols;
            var gy = i / Cols;

            var cellRect = new System.Drawing.Rectangle(gx * cellW, gy * cellH, cellW, cellH);

            var innerRect = new System.Drawing.Rectangle(
                cellRect.X + padX,
                cellRect.Y + padTop,
                cellRect.Width - padX * 2,
                cellRect.Height - padTop - padBottom);


            System.Windows.Forms.TextRenderer.DrawText(
                g,
                ch.ToString(),
                font,
                innerRect,
                Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.GlyphOverhangPadding);

        }

        var lockRect = new System.Drawing.Rectangle(0, 0, atlasW, atlasH);
        var data = bmp.LockBits(lockRect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            var bytes = new byte[atlasW * atlasH * 4];
            unsafe
            {
                var srcBase = (byte*)data.Scan0;
                var di = 0;

                for (var y = 0; y < atlasH; y++)
                {
                    var row = srcBase + (y * data.Stride);

                    for (var x = 0; x < atlasW; x++)
                    {
                        // Format32bppArgb = BGRA in memory
                        var bi = x * 4;
                        var b = row[bi + 0];
                        var gg = row[bi + 1];
                        var r = row[bi + 2];
                        var a = row[bi + 3];

                        bytes[di + 0] = r;
                        bytes[di + 1] = gg;
                        bytes[di + 2] = b;
                        bytes[di + 3] = a;
                        di += 4;
                    }
                }
            }

            return bytes;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }


    public void Dispose()
    {
        // Shared atlas resources are cached per GraphicsDevice and intentionally kept alive
        // for the process lifetime to avoid expensive glyph atlas regeneration during resize-driven
        // renderer reinitialization.
    }
}
