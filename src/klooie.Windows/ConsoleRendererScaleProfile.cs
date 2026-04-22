using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace klooie;

public sealed class ConsoleRendererScaleProfile
{
    public required string Name { get; init; }
    public required int CellPixelWidth { get; init; }
    public required float FontPixelSize { get; init; }
    public required string FontFamilyName { get; init; }
    public required float TextOffsetX { get; init; }
    public required float TextOffsetY { get; init; }
    public required float TextScaleX { get; init; }
    public required float TextScaleY { get; init; }
    public TextRenderingHint TextRenderingHint { get; init; } = TextRenderingHint.AntiAliasGridFit;
    public PixelOffsetMode PixelOffsetMode { get; init; } = PixelOffsetMode.HighQuality;
    public SmoothingMode SmoothingMode { get; init; } = SmoothingMode.None;
    public InterpolationMode InterpolationMode { get; init; } = InterpolationMode.Default;
    public CompositingMode CompositingMode { get; init; } = CompositingMode.SourceOver;
    public CompositingQuality CompositingQuality { get; init; } = CompositingQuality.HighSpeed;
    public int TextContrast { get; init; } = 4;

    public int CellPixelHeight => CellPixelWidth * 2;

    public static ConsoleRendererScaleProfile High { get; } = new ConsoleRendererScaleProfile
    {
        Name = "High",
        CellPixelWidth = 32,
        FontPixelSize = 58f,
        FontFamilyName = "Consolas",
        TextOffsetX = 1f,
        TextOffsetY = -10f,
        TextScaleX = .95f,
        TextScaleY = 1f,
    };

    public static ConsoleRendererScaleProfile Medium { get; } = new ConsoleRendererScaleProfile
    {
        Name = "Medium",
        CellPixelWidth = 16,
        FontPixelSize = 27.95f,
        FontFamilyName = "Consolas",
        TextOffsetX = .25f,
        TextOffsetY = -7f,
        TextScaleX = 1.004f,
        TextScaleY = 1f,
    };

    public static ConsoleRendererScaleProfile Low { get; } = new ConsoleRendererScaleProfile
    {
        Name = "Low",
        CellPixelWidth = 10,
        FontPixelSize = 19f,
        FontFamilyName = "Consolas",
        TextOffsetX = 0.3f,
        TextOffsetY = -8.5f,
        TextScaleX = 1f,
        TextScaleY = 1f,
        TextRenderingHint = TextRenderingHint.AntiAliasGridFit,
        PixelOffsetMode = PixelOffsetMode.HighQuality,
        SmoothingMode = SmoothingMode.AntiAlias,
        InterpolationMode = InterpolationMode.Default,
        CompositingQuality = CompositingQuality.HighSpeed,
        TextContrast = 0,
    };
}