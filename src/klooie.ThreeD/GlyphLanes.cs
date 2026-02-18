namespace klooie;

public enum GlyphLane : byte { Flat = 0, ThreeD = 1 }

public readonly struct FlatGlyphRef
{
    public readonly ushort Page;   // 0 for now
    public readonly ushort Index;  // 0..N
    public FlatGlyphRef(ushort page, ushort index) { Page = page; Index = index; }
}

public interface IGlyphLaneSelector
{
    GlyphLane Select(char value);
}

public interface IFlatGlyphMapper
{
    FlatGlyphRef Map(char value);
}

public sealed class DefaultGlyphLaneSelector : IGlyphLaneSelector
{
    public static readonly DefaultGlyphLaneSelector Instance = new DefaultGlyphLaneSelector();
    private DefaultGlyphLaneSelector() { }
    public GlyphLane Select(char value)
    {
        var fullSupportFor3D = ShapeRegistry.Instance.SupportsWithoutFallback(value);
        if(fullSupportFor3D) return GlyphLane.ThreeD;

        if (char.IsAscii(value)) return GlyphLane.Flat;

        return GlyphLane.ThreeD; // will fallback to default shape (probably cube)
    }
}

public sealed class Ascii256GlyphMapper : IFlatGlyphMapper
{
    public static readonly Ascii256GlyphMapper Instance = new Ascii256GlyphMapper();
    private Ascii256GlyphMapper() { }

    public FlatGlyphRef Map(char value)
    {
        // Today: your atlas is 16x16 for 0..255.
        // For anything else, fall back to '?' (63) or ' ' (32).
        var idx = value <= 255 ? (ushort)value : (ushort)'?';
        return new FlatGlyphRef(page: 0, index: idx);
    }
}