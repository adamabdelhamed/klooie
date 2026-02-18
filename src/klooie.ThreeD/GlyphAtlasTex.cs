using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
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

    private readonly Veldrid.GraphicsDevice gd;

    public GlyphAtlasTex(Veldrid.GraphicsDevice gd, Veldrid.ResourceFactory factory, int cellW, int supersample)
    {
        this.gd = gd;

        supersample = Math.Max(1, supersample);

        // IMPORTANT: Internal atlas cell size is supersampled
        CellW = Math.Max(4, cellW) * supersample;
        CellH = CellW * 2;

        AtlasW = CellW * Cols;
        AtlasH = CellH * Rows;

        Tex = factory.CreateTexture(Veldrid.TextureDescription.Texture2D(
            (uint)AtlasW, (uint)AtlasH, 1, 1,
            Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,
            Veldrid.TextureUsage.Sampled));

        View = factory.CreateTextureView(Tex);

        var rgba = BuildAtlasRgba();
        gd.UpdateTexture(Tex, rgba, 0, 0, 0, (uint)AtlasW, (uint)AtlasH, 1, 0, 0);
    }

    private byte[] BuildAtlasRgba()
    {
        using var bmp = new Bitmap(AtlasW, AtlasH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
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

        var fontPx = (int)(CellH * 0.75f);
        using var font = new Font("Consolas", fontPx, FontStyle.Regular, GraphicsUnit.Pixel);


        using var brush = new SolidBrush(Color.White);
        var padX = Math.Max(1, CellW / 16);
        var padTop = Math.Max(1, CellH / 24);
        var padBottom = Math.Max(4, CellH / 12);


        for (var i = 0; i < 256; i++)
        {
            var ch = (char)i;

            // Skip controls for now
            if (char.IsControl(ch)) continue;

            var gx = i % Cols;
            var gy = i / Cols;

            var cellRect = new System.Drawing.Rectangle(gx * CellW, gy * CellH, CellW, CellH);

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

        var lockRect = new System.Drawing.Rectangle(0, 0, AtlasW, AtlasH);
        var data = bmp.LockBits(lockRect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            var bytes = new byte[AtlasW * AtlasH * 4];
            unsafe
            {
                var srcBase = (byte*)data.Scan0;
                var di = 0;

                for (var y = 0; y < AtlasH; y++)
                {
                    var row = srcBase + (y * data.Stride);

                    for (var x = 0; x < AtlasW; x++)
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
        View.Dispose();
        Tex.Dispose();
    }
}
