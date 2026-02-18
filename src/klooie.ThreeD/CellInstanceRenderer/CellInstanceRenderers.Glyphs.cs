using System.Numerics;
using Veldrid;

namespace klooie;

public sealed partial class CellInstancedRenderer
{
    private DeviceBuffer glyphUbo;
    private GlyphAtlasTex glyphAtlas;

    private int currentGlyphCellW = -1;
    private int currentPresentCellWpx;

    private readonly struct GlyphUniform
    {
        public readonly Vector2 AtlasCellPix;
        public readonly Vector2 AtlasSize;
        public readonly Vector2 PadPix;
        public readonly Vector2 InnerPix;
        public readonly Vector4 Shadow; // xy=offset px in atlas space, z=opacity

        public GlyphUniform(Vector2 atlasCellPix, Vector2 atlasSize, Vector2 padPix, Vector2 innerPix, Vector4 shadow)
        {
            AtlasCellPix = atlasCellPix;
            AtlasSize = atlasSize;
            PadPix = padPix;
            InnerPix = innerPix;
            Shadow = shadow;
        }
    }

    private static uint PackGlyph(FlatGlyphRef g) => (uint)(g.Index | (g.Page << 16));
    private static uint GlyphIndex(uint packed) => packed & 0xFFFFu;
    private static uint GlyphPage(uint packed) => packed >> 16;

    private void EnsureGlyphAtlas(int targetCellW)
    {
        if (glyphAtlas != null)
        {
            var dw = Math.Abs(targetCellW - currentGlyphCellW);
            if (dw <= 1) return;
        }

        glyphAtlas?.Dispose();
        glyphAtlas = new GlyphAtlasTex(gd, factory, targetCellW, supersample: 3);
        currentGlyphCellW = targetCellW;

        resourceSet?.Dispose();
        resourceSet = null;
    }

    private void UpdateGlyphUbo()
    {
        if (glyphAtlas == null) return;

        var atlasCellPix = new Vector2(glyphAtlas.CellW, glyphAtlas.CellH);
        var atlasSize = new Vector2(glyphAtlas.AtlasW, glyphAtlas.AtlasH);

        var padX = Math.Max(1, glyphAtlas.CellW / 16);
        var padTop = Math.Max(1, glyphAtlas.CellH / 24);
        var padBottom = Math.Max(4, glyphAtlas.CellH / 12);

        var innerW = glyphAtlas.CellW - padX * 2;
        var innerH = glyphAtlas.CellH - padTop - padBottom;

        var shadowOffsetPix = Vector2.Zero;      // all-sides
        var shadowOpacity = 0.55f;
        var shadowRadiusPix = 2.75f;
        var shadow = new Vector4(shadowOffsetPix, shadowOpacity, shadowRadiusPix);

        gd.UpdateBuffer(glyphUbo, 0, new GlyphUniform(
            atlasCellPix,
            atlasSize,
            new Vector2(padX, padTop),
            new Vector2(innerW, innerH),
            shadow));
    }
}
