using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;
using System.Windows.Forms;
using Veldrid;

namespace klooie;

/// <summary>
/// Tunable parameters:
/// - MaskWidth/MaskHeight: raster resolution for glyph extraction (higher = more detail, more triangles).
/// - PaddingX/PaddingY: empty margin inside the 1x2 world cell to prevent neighboring glyphs from visually touching.
/// - ExtrusionDepth: Z thickness of the generated mesh.
/// - FontName/FontStyle/FontScale: text style used for rasterization.
/// - AlphaThreshold: alpha cutoff for binary mask generation.
///
/// Algorithm overview:
/// 1) Rasterize the glyph into a high-resolution transparent bitmap.
/// 2) Threshold alpha into a binary mask.
/// 3) Merge contiguous filled pixels into coarse rectangles for front/back caps.
/// 4) Build front + back faces from merged rectangles.
/// 5) Build extruded side walls by emitting quads on mask boundary edges.
/// 6) Cache generated mesh data per glyph/options and upload lazily on first use.
///
/// Known limitations:
/// - This implementation intentionally uses rectangle-merging mask extrusion instead of contour + polygon triangulation.
/// - Holes are represented correctly as empty regions, but cap tessellation follows mask rectangles (not minimal polygon triangulation).
/// - Very small raster resolutions can introduce staircase artifacts.
/// </summary>
public sealed class GlyphExtrusionShape : IShape3D
{
    public readonly record struct GlyphExtrusionOptions(
        int MaskWidth,
        int MaskHeight,
        float PaddingX,
        float PaddingY,
        float ExtrusionDepth,
        string FontName,
        FontStyle FontStyle,
        float FontScale,
        byte AlphaThreshold)
    {
        public static GlyphExtrusionOptions Default => new(
            MaskWidth: 64,
            MaskHeight: 128,
            PaddingX: 0f,
            PaddingY: 0f,
            ExtrusionDepth: 0.3f,
            FontName: "Consolas",
            FontStyle: FontStyle.Bold,
            FontScale: 1f,
            AlphaThreshold: 96);
    }

    private readonly struct ShapeVertex
    {
        public const uint SizeInBytes = 24;
        public readonly Vector3 Pos;
        public readonly Vector3 Nrm;
        public ShapeVertex(Vector3 pos, Vector3 nrm) { Pos = pos; Nrm = nrm; }
    }

    private readonly struct GlyphMeshData
    {
        public readonly ShapeVertex[] Vertices;
        public readonly uint[] Indices;

        public GlyphMeshData(ShapeVertex[] vertices, uint[] indices)
        {
            Vertices = vertices;
            Indices = indices;
        }
    }

    private readonly struct RectI
    {
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;

        public RectI(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }

    private static readonly Dictionary<string, GlyphMeshData> MeshCache = new();

    private readonly char glyph;
    private readonly GlyphExtrusionOptions options;

    private DeviceBuffer vb;
    private DeviceBuffer ib;
    private bool created;
    private uint indexCount;

    public GlyphExtrusionShape(char glyph, GlyphExtrusionOptions? options = null)
    {
        this.glyph = glyph;
        this.options = options ?? GlyphExtrusionOptions.Default;
    }

    public VertexLayoutDescription VertexLayout => new VertexLayoutDescription(
        new VertexElementDescription("Pos", VertexElementSemantic.Position, VertexElementFormat.Float3),
        new VertexElementDescription("Nrm", VertexElementSemantic.Normal, VertexElementFormat.Float3))
    {
        Stride = ShapeVertex.SizeInBytes,
        InstanceStepRate = 0
    };

    public DeviceBuffer VertexBuffer => vb;
    public DeviceBuffer IndexBuffer => ib;
    public IndexFormat IndexFormat => IndexFormat.UInt32;
    public uint IndexCount => indexCount;

    public void EnsureResources(GraphicsDevice gd, ResourceFactory factory)
    {
        if (created) return;
        created = true;

        var mesh = GetOrBuildMesh();
        indexCount = (uint)mesh.Indices.Length;

        vb = factory.CreateBuffer(new BufferDescription((uint)(mesh.Vertices.Length * ShapeVertex.SizeInBytes), BufferUsage.VertexBuffer));
        ib = factory.CreateBuffer(new BufferDescription((uint)(mesh.Indices.Length * sizeof(uint)), BufferUsage.IndexBuffer));

        gd.UpdateBuffer(vb, 0, mesh.Vertices);
        gd.UpdateBuffer(ib, 0, mesh.Indices);
    }

    public void Dispose()
    {
        vb?.Dispose(); vb = null;
        ib?.Dispose(); ib = null;
        created = false;
        indexCount = 0;
    }

    private GlyphMeshData GetOrBuildMesh()
    {
        var cacheKey = $"{glyph}|{options.MaskWidth}x{options.MaskHeight}|{options.PaddingX:F4}|{options.PaddingY:F4}|{options.ExtrusionDepth:F4}|{options.FontName}|{(int)options.FontStyle}|{options.FontScale:F4}|{options.AlphaThreshold}";

        if (MeshCache.TryGetValue(cacheKey, out var cached)) return cached;

        var mask = RasterizeGlyphMask();
        var rectangles = MergeMaskToRectangles(mask);
        var mesh = BuildExtrudedMesh(mask, rectangles);

        MeshCache[cacheKey] = mesh;
        return mesh;
    }

    private bool[,] RasterizeGlyphMask()
    {
        var w = Math.Max(8, options.MaskWidth);
        var h = Math.Max(8, options.MaskHeight);

        // Overscan so descenders/overhangs don't get constrained by the target cell.
        // 2x is usually enough; 3x if you use very heavy weights or odd fonts.
        var ow = w * 2;
        var oh = h * 2;

        using var bmp = new Bitmap(ow, oh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var fontPx = Math.Max(8, (int)(h * options.FontScale));
        using var font = new Font(options.FontName, fontPx, options.FontStyle, GraphicsUnit.Pixel);

        // Draw into the oversized canvas. Don't try to "center" perfectly; we're going to re-pack by bounds anyway.
        var drawRect = new System.Drawing.Rectangle(0, 0, ow, oh);

        TextRenderer.DrawText(
            g,
            glyph.ToString(),
            font,
            drawRect,
            Color.White,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding |
            TextFormatFlags.GlyphOverhangPadding |
            TextFormatFlags.NoPrefix);

        // Threshold into an overscan mask
        var omask = new bool[ow, oh];
        int minX = ow, minY = oh, maxX = -1, maxY = -1;

        for (var y = 0; y < oh; y++)
        {
            for (var x = 0; x < ow; x++)
            {
                var a = bmp.GetPixel(x, y).A;
                if (a >= options.AlphaThreshold)
                {
                    omask[x, y] = true;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }

        // If nothing was drawn, return empty (caller already has fallback quad)
        if (maxX < minX || maxY < minY) return new bool[w, h];

        // Tight bounds (inclusive) => width/height
        var bw = (maxX - minX) + 1;
        var bh = (maxY - minY) + 1;

        // Target inner area after padding (in pixels)
        var padXPx = Math.Max(0, (int)(w * options.PaddingX));
        var padYPx = Math.Max(0, (int)(h * options.PaddingY));
        var innerW = Math.Max(1, w - (padXPx * 2));
        var innerH = Math.Max(1, h - (padYPx * 2));

        // Optional descender gutter: reserve a bit of empty space under the glyph
        // so descenders don't look like they're sitting on the "floor".
        // Tune 0..(innerH/10). Start small.
        var descenderGutter = Math.Max(0, innerH / 24);

        // We are NOT scaling here (your mesh already maps pixels to world).
        // We just pack/crop to innerW/innerH with bottom docking.
        var dst = new bool[w, h];

        // Horizontal center stays the same
        var dstX0 = padXPx + (innerW - bw) / 2;

        // ---- NEW VERTICAL LOGIC ----

        // If the glyph contains a descender, we shift it down slightly
        // instead of raising the whole glyph.
        bool hasDescender = glyph is 'g' or 'j' or 'p' or 'q' or 'y';

        // Base vertical centering
        var dstY0 = padYPx + (innerH - bh) / 2;

        // Descender bias: push those glyphs slightly downward
        // This prevents the body from appearing raised.
        if (hasDescender)
        {
            var descenderShift = innerH / 16; // tune 10–16 range
            dstY0 += descenderShift;
        }

        for (var sy = 0; sy < bh; sy++)
        {
            var dy = dstY0 + sy;
            if (dy < 0 || dy >= h) continue;

            var oy = minY + sy;
            for (var sx = 0; sx < bw; sx++)
            {
                var dx = dstX0 + sx;
                if (dx < 0 || dx >= w) continue;

                var ox = minX + sx;
                if (omask[ox, oy]) dst[dx, dy] = true;
            }
        }

        PreserveThinStrokes(dst);
        return dst;
    }

    private static void PreserveThinStrokes(bool[,] mask)
    {
        var w = mask.GetLength(0);
        var h = mask.GetLength(1);
        var copy = (bool[,])mask.Clone();

        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                if (!copy[x, y]) continue;

                var hasHorizontal = copy[x - 1, y] || copy[x + 1, y];
                var hasVertical = copy[x, y - 1] || copy[x, y + 1];
                if (hasHorizontal && hasVertical) continue;

                // Tiny, constrained dilation for single-pixel strokes/terminals.
                if (!hasHorizontal && hasVertical)
                {
                    mask[x - 1, y] = true;
                    mask[x + 1, y] = true;
                }
                else if (hasHorizontal && !hasVertical)
                {
                    mask[x, y - 1] = true;
                    mask[x, y + 1] = true;
                }
            }
        }
    }


    private static List<RectI> MergeMaskToRectangles(bool[,] mask)
    {
        var w = mask.GetLength(0);
        var h = mask.GetLength(1);
        var visited = new bool[w, h];
        var rects = new List<RectI>();

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!mask[x, y] || visited[x, y]) continue;

                var rectW = 1;
                while ((x + rectW) < w && mask[x + rectW, y] && !visited[x + rectW, y]) rectW++;

                var rectH = 1;
                var canGrow = true;
                while (canGrow && (y + rectH) < h)
                {
                    for (var tx = x; tx < x + rectW; tx++)
                    {
                        if (!mask[tx, y + rectH] || visited[tx, y + rectH])
                        {
                            canGrow = false;
                            break;
                        }
                    }

                    if (canGrow) rectH++;
                }

                for (var yy = y; yy < y + rectH; yy++)
                {
                    for (var xx = x; xx < x + rectW; xx++) visited[xx, yy] = true;
                }

                rects.Add(new RectI(x, y, rectW, rectH));
            }
        }

        return rects;
    }

    private GlyphMeshData BuildExtrudedMesh(bool[,] mask, List<RectI> rectangles)
    {
        var w = mask.GetLength(0);
        var h = mask.GetLength(1);

        var left = -0.5f + options.PaddingX;
        var right = 0.5f - options.PaddingX;
        var bottom = -1f + options.PaddingY;
        var top = 1f - options.PaddingY;

        if (right <= left || top <= bottom)
        {
            left = -0.48f;
            right = 0.48f;
            bottom = -0.96f;
            top = 0.96f;
        }

        var cellW = (right - left) / w;
        var cellH = (top - bottom) / h;

        var zFront = options.ExtrusionDepth * 0.5f;
        var zBack = -zFront;

        var verts = new List<ShapeVertex>(4096);
        var indices = new List<uint>(8192);

        foreach (var rect in rectangles)
        {
            var x0 = left + (rect.X * cellW);
            var x1 = left + ((rect.X + rect.W) * cellW);
            var y0 = bottom + (rect.Y * cellH);
            var y1 = bottom + ((rect.Y + rect.H) * cellH);

            AddQuad(verts, indices,
                new Vector3(x0, y0, zFront),
                new Vector3(x1, y0, zFront),
                new Vector3(x1, y1, zFront),
                new Vector3(x0, y1, zFront),
                new Vector3(0, 0, 1));

            AddQuad(verts, indices,
                new Vector3(x0, y0, zBack),
                new Vector3(x0, y1, zBack),
                new Vector3(x1, y1, zBack),
                new Vector3(x1, y0, zBack),
                new Vector3(0, 0, -1));
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!mask[x, y]) continue;

                var x0 = left + (x * cellW);
                var x1 = x0 + cellW;
                var y0 = bottom + (y * cellH);
                var y1 = y0 + cellH;

                if (x == 0 || !mask[x - 1, y])
                {
                    AddQuad(verts, indices,
                        new Vector3(x0, y0, zBack),
                        new Vector3(x0, y0, zFront),
                        new Vector3(x0, y1, zFront),
                        new Vector3(x0, y1, zBack),
                        new Vector3(-1, 0, 0));
                }

                if (x == w - 1 || !mask[x + 1, y])
                {
                    AddQuad(verts, indices,
                        new Vector3(x1, y0, zFront),
                        new Vector3(x1, y0, zBack),
                        new Vector3(x1, y1, zBack),
                        new Vector3(x1, y1, zFront),
                        new Vector3(1, 0, 0));
                }

                if (y == 0 || !mask[x, y - 1])
                {
                    AddQuad(verts, indices,
                        new Vector3(x0, y1, zFront),
                        new Vector3(x1, y1, zFront),
                        new Vector3(x1, y1, zBack),
                        new Vector3(x0, y1, zBack),
                        new Vector3(0, 1, 0));
                }

                if (y == h - 1 || !mask[x, y + 1])
                {
                    AddQuad(verts, indices,
                        new Vector3(x0, y0, zBack),
                        new Vector3(x1, y0, zBack),
                        new Vector3(x1, y0, zFront),
                        new Vector3(x0, y0, zFront),
                        new Vector3(0, -1, 0));
                }
            }
        }

        if (indices.Count == 0)
        {
            AddQuad(verts, indices,
                new Vector3(-0.02f, -0.02f, zFront),
                new Vector3(0.02f, -0.02f, zFront),
                new Vector3(0.02f, 0.02f, zFront),
                new Vector3(-0.02f, 0.02f, zFront),
                new Vector3(0, 0, 1));

            AddQuad(verts, indices,
                new Vector3(-0.02f, -0.02f, zBack),
                new Vector3(-0.02f, 0.02f, zBack),
                new Vector3(0.02f, 0.02f, zBack),
                new Vector3(0.02f, -0.02f, zBack),
                new Vector3(0, 0, -1));
        }

        return new GlyphMeshData(verts.ToArray(), indices.ToArray());
    }

    private static void AddQuad(List<ShapeVertex> verts, List<uint> indices, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal)
    {
        var baseIndex = (uint)verts.Count;
        verts.Add(new ShapeVertex(v0, normal));
        verts.Add(new ShapeVertex(v1, normal));
        verts.Add(new ShapeVertex(v2, normal));
        verts.Add(new ShapeVertex(v3, normal));

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);

        indices.Add(baseIndex + 0);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }
}
