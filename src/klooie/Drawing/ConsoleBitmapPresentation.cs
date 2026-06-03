namespace klooie;

public interface IConsoleBitmapPresentation
{
    ILifetime ScaleRegion(Rect sourceRegion, float scale, RegionScaleOptions? options = null, ILifetime? lifetime = null);
    ILifetime ScaleRegion(Func<Rect> sourceRegion, float scale, RegionScaleOptions? options = null, ILifetime? lifetime = null);
    ILifetime FocusRegion(Rect sourceRegion, FocusRegionOptions? options = null, ILifetime? lifetime = null);
    ILifetime FocusControl(ConsoleControl control, Func<ConsoleControl, Rect>? sourceRegion = null, FocusRegionOptions? options = null, ILifetime? lifetime = null);
}

public enum ConsoleBitmapPresentationAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

public sealed class RegionScaleOptions
{
    public ConsoleBitmapPresentationAnchor Anchor { get; init; } = ConsoleBitmapPresentationAnchor.TopLeft;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int Priority { get; init; }
}

public sealed class FocusRegionOptions
{
    public ConsoleBitmapPresentationAnchor Anchor { get; init; } = ConsoleBitmapPresentationAnchor.Center;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public float Padding { get; init; } = .06f;
    public int Priority { get; init; }
    public int AnimationMilliseconds { get; init; } = 450;
}

public sealed class ConsoleBitmapPresentation : IConsoleBitmapPresentation
{
    private readonly object sync = new();
    private readonly List<ConsoleBitmapRegionScaleRequest> scaledRegions = new();
    private readonly List<ConsoleBitmapFocusRegionRequest> focusRegions = new();
    private int nextId = 1;

    public ILifetime ScaleRegion(Rect sourceRegion, float scale, RegionScaleOptions? options = null, ILifetime? lifetime = null)
    {
        ValidateRegion(sourceRegion);
        return ScaleRegion(() => sourceRegion, scale, options, lifetime);
    }

    public ILifetime ScaleRegion(Func<Rect> sourceRegion, float scale, RegionScaleOptions? options = null, ILifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(sourceRegion);
        if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(scale));

        options ??= new RegionScaleOptions();
        var region = new ConsoleBitmapRegionScaleRequest(nextId++, sourceRegion, scale, options.Anchor, options.OffsetX, options.OffsetY, options.Priority);
        lock (sync) scaledRegions.Add(region);
        return TrackLifetime(lifetime, region, static (me, value) => me.RemoveScale(value));
    }

    public ILifetime FocusRegion(Rect sourceRegion, FocusRegionOptions? options = null, ILifetime? lifetime = null)
    {
        ValidateRegion(sourceRegion);

        options ??= new FocusRegionOptions();
        var id = nextId++;
        var region = new ConsoleBitmapFocusRegionRequest(id, () => sourceRegion, options.Anchor, options.OffsetX, options.OffsetY, Math.Clamp(options.Padding, 0, .45f), options.Priority, Math.Max(1, options.AnimationMilliseconds));
        lock (sync) focusRegions.Add(region);
        return TrackLifetime(lifetime, region, static (me, value) => me.RemoveFocus(value));
    }

    public ILifetime FocusControl(ConsoleControl control, Func<ConsoleControl, Rect>? sourceRegion = null, FocusRegionOptions? options = null, ILifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        sourceRegion ??= static c => c.AbsoluteBounds.ToRect();
        options ??= new FocusRegionOptions();
        var id = nextId++;
        var region = new ConsoleBitmapFocusRegionRequest(id, () => sourceRegion(control), options.Anchor, options.OffsetX, options.OffsetY, Math.Clamp(options.Padding, 0, .45f), options.Priority, Math.Max(1, options.AnimationMilliseconds));
        lock (sync) focusRegions.Add(region);
        return TrackLifetime(lifetime ?? control, region, static (me, value) => me.RemoveFocus(value));
    }

    public ConsoleBitmapPresentationFrame CreateFrame()
    {
        lock (sync)
        {
            if (scaledRegions.Count == 0 && focusRegions.Count == 0) return ConsoleBitmapPresentationFrame.Empty;
            return new ConsoleBitmapPresentationFrame
            {
                ScaledRegions = scaledRegions.OrderBy(r => r.Priority).ThenBy(r => r.Id).Select(static r => r.CreateFrameRegion()).ToArray(),
                FocusRegions = focusRegions.OrderBy(r => r.Priority).ThenBy(r => r.Id).Select(static r => r.CreateFrameRegion()).ToArray()
            };
        }
    }

    private ILifetime TrackLifetime<T>(ILifetime? lifetime, T value, Action<ConsoleBitmapPresentation, T> cleanup)
    {
        var ownedLifetime = lifetime ?? DefaultRecyclablePool.Instance.Rent();
        ownedLifetime.OnDisposedOrNow(this, me => cleanup(me, value));
        return ownedLifetime;
    }

    private void RemoveScale(ConsoleBitmapRegionScaleRequest region)
    {
        lock (sync) scaledRegions.Remove(region);
    }

    private void RemoveFocus(ConsoleBitmapFocusRegionRequest region)
    {
        lock (sync) focusRegions.Remove(region);
    }

    internal static void ValidateRegion(Rect sourceRegion)
    {
        if (sourceRegion.Width <= 0) throw new ArgumentOutOfRangeException(nameof(sourceRegion), "The source region width must be positive.");
        if (sourceRegion.Height <= 0) throw new ArgumentOutOfRangeException(nameof(sourceRegion), "The source region height must be positive.");
    }
}

internal sealed class ConsoleBitmapRegionScaleRequest
{
    private readonly Func<Rect> sourceRegion;

    public int Id { get; }
    public float Scale { get; }
    public ConsoleBitmapPresentationAnchor Anchor { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
    public int Priority { get; }

    public ConsoleBitmapRegionScaleRequest(int id, Func<Rect> sourceRegion, float scale, ConsoleBitmapPresentationAnchor anchor, int offsetX, int offsetY, int priority)
    {
        Id = id;
        this.sourceRegion = sourceRegion;
        Scale = scale;
        Anchor = anchor;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Priority = priority;
    }

    public ConsoleBitmapRegionScale CreateFrameRegion()
    {
        var region = sourceRegion();
        ConsoleBitmapPresentation.ValidateRegion(region);
        return new ConsoleBitmapRegionScale(Id, ConsoleBitmapPresentationRect.From(region), Scale, Anchor, OffsetX, OffsetY, Priority);
    }
}

internal sealed class ConsoleBitmapFocusRegionRequest
{
    private readonly Func<Rect> sourceRegion;

    public int Id { get; }
    public ConsoleBitmapPresentationAnchor Anchor { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
    public float Padding { get; }
    public int Priority { get; }
    public int AnimationMilliseconds { get; }

    public ConsoleBitmapFocusRegionRequest(int id, Func<Rect> sourceRegion, ConsoleBitmapPresentationAnchor anchor, int offsetX, int offsetY, float padding, int priority, int animationMilliseconds)
    {
        Id = id;
        this.sourceRegion = sourceRegion;
        Anchor = anchor;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Padding = padding;
        Priority = priority;
        AnimationMilliseconds = animationMilliseconds;
    }

    public ConsoleBitmapFocusRegion CreateFrameRegion()
    {
        var region = sourceRegion();
        ConsoleBitmapPresentation.ValidateRegion(region);
        return new ConsoleBitmapFocusRegion(Id, ConsoleBitmapPresentationRect.From(region), Anchor, OffsetX, OffsetY, Padding, Priority, AnimationMilliseconds);
    }
}

public sealed class ConsoleBitmapPresentationFrame
{
    public static ConsoleBitmapPresentationFrame Empty { get; } = new()
    {
        ScaledRegions = Array.Empty<ConsoleBitmapRegionScale>(),
        FocusRegions = Array.Empty<ConsoleBitmapFocusRegion>()
    };

    public required ConsoleBitmapRegionScale[] ScaledRegions { get; init; }
    public required ConsoleBitmapFocusRegion[] FocusRegions { get; init; }
}

public sealed class ConsoleBitmapPresentationRect
{
    public required int Left { get; init; }
    public required int Top { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    public static ConsoleBitmapPresentationRect From(Rect rect) => new()
    {
        Left = rect.Left,
        Top = rect.Top,
        Width = rect.Width,
        Height = rect.Height
    };
}

public sealed record ConsoleBitmapRegionScale(
    int Id,
    ConsoleBitmapPresentationRect SourceRegion,
    float Scale,
    ConsoleBitmapPresentationAnchor Anchor,
    int OffsetX,
    int OffsetY,
    int Priority);

public sealed record ConsoleBitmapFocusRegion(
    int Id,
    ConsoleBitmapPresentationRect SourceRegion,
    ConsoleBitmapPresentationAnchor Anchor,
    int OffsetX,
    int OffsetY,
    float Padding,
    int Priority,
    int AnimationMilliseconds);
