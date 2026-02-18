using Veldrid;

namespace klooie;

public sealed class ShapeRegistry : IDisposable
{
    private readonly List<IShape3D> shapes = new();
    private readonly Dictionary<char, ushort> glyphToId = new();
    private readonly ushort fallbackId;

    public VertexLayoutDescription VertexLayout { get; }

    public static readonly ShapeRegistry Instance = new ShapeRegistry();

    private ShapeRegistry()
    {
        var fallback = new Cube();
        fallbackId = RegisterShape(fallback);
        VertexLayout = fallback.VertexLayout;

        Register(('●', new Sphere(radius: 0.40f)),
                ('•', new Sphere(radius: 0.3f)),
                ('·', new Sphere(radius: 0.2f)),
                ('|', new Cuboid(width: 0.35f, height: 1.8f, depth: 0.30f)),
                ('-', new Cuboid(width: 0.9f, height: 0.35f, depth: 0.30f)),
                ('/', new Cuboid(width: 0.35f, height: 1.8f, depth: 0.30f, rotationZ: MathF.PI / 5f)),
                ('\\', new Cuboid(width: 0.35f, height: 1.8f, depth: 0.30f, rotationZ: -MathF.PI / 5f)),
                ('o', new Torus(majorRadius: 0.35f, minorRadius: 0.14f, majorSegments: 64, minorSegments: 48, yOffset: -0.15f)),
                ('←', new ArrowPrism(rotationZ: MathF.PI * 0.5f)),
                ('→', new ArrowPrism(rotationZ: -MathF.PI * 0.5f)),
                ('↑', new ArrowPrism(rotationZ: 0f)),
                ('↓', new ArrowPrism(rotationZ: MathF.PI)));

        Register('♩', '♪', '♫', '♬');
        Register('ᚠ', 'ᚢ', 'ᛁ', 'ᚱ', 'ᛚ', 'ᛜ', 'ᛉ', 'ᛇ');
        Register('⋆', '★' , '✶');
        var glyphOptions = GlyphExtrusionShape.GlyphExtrusionOptions.Default;
        RegisterGlyphRange('A', 'Z', c => new GlyphExtrusionShape(c, glyphOptions));
        RegisterGlyphRange('a', 'z', c => new GlyphExtrusionShape(c, glyphOptions));
        RegisterGlyphRange('0', '9', c => new GlyphExtrusionShape(c, glyphOptions));
    }

    private void Register(params char[] glyphs)
    {
        for (int i = 0; i < glyphs.Length; i++) Register(glyphs[i]);
    }

    private ushort Register(char glyph) => Register(glyph, new GlyphExtrusionShape(glyph, GlyphExtrusionShape.GlyphExtrusionOptions.Default));

    private ushort Register(char glyph, IShape3D shape)
    {
        var id = RegisterShape(shape);
        RegisterGlyph(glyph, id);
        return id;
    }

    private void Register(params (char glyph, IShape3D shape)[] items)
    {
        for (var i = 0; i < items.Length; i++) Register(items[i].glyph, items[i].shape);
    }

    private void RegisterGlyphRange(char startInclusive, char endInclusive, Func<char, IShape3D> shapeFactory)
    {
        for (var glyph = startInclusive; glyph <= endInclusive; glyph++) Register(glyph, shapeFactory(glyph));
    }

    public ushort RegisterShape(IShape3D shape)
    {
        var id = (ushort)shapes.Count;
        shapes.Add(shape);
        return id;
    }

    public void RegisterGlyph(char glyph, ushort shapeId) => glyphToId[glyph] = shapeId;

    public ushort ResolveId(char glyph) => glyphToId.TryGetValue(glyph, out var id) ? id : fallbackId;

    public bool SupportsWithoutFallback(char glyph) => glyphToId.ContainsKey(glyph);

    public IShape3D GetShape(ushort shapeId) => shapes[shapeId];

    public int Count => shapes.Count;

    public void EnsureResources(GraphicsDevice gd, ResourceFactory factory)
    {
        for (var i = 0; i < shapes.Count; i++) shapes[i].EnsureResources(gd, factory);
    }

    public void Dispose()
    {
        for (var i = 0; i < shapes.Count; i++) shapes[i].Dispose();
        shapes.Clear();
        glyphToId.Clear();
    }
}
