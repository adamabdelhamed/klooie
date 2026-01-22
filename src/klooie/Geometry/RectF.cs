using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace klooie;
[ArgReviverType]
public readonly struct RectF : IEquatable<RectF>, ICollidable
{
    public readonly float Left;
    public readonly float Top;
    public readonly float Width;
    public readonly float Height;

    public float Right => Left + Width;
    public float Bottom => Top + Height;
    public float CenterX => Left + Width * 0.5f;
    public float CenterY => Top + Height * 0.5f;
    public LocF Center => new LocF(CenterX, CenterY);
    public LocF TopLeft => new LocF(Left, Top);
    public LocF TopRight => new LocF(Right, Top);
    public LocF BottomLeft => new LocF(Left, Bottom);
    public LocF BottomRight => new LocF(Right, Bottom);

    public Edge LeftEdge => new Edge(Left, Top, Left, Bottom);
    public Edge RightEdge => new Edge(Right, Top, Right, Bottom);
    public Edge TopEdge => new Edge(Left, Top, Right, Top);
    public Edge BottomEdge => new Edge(Left, Bottom, Right, Bottom);

    // Keeping the public name "Hypotenous" as-is (typo preserved)
    public float Hypotenous => MathF.Sqrt(Width * Width + Height * Height);

    public RectF Bounds => this;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanCollideWith(ICollidable other) => true;

    public RectF(float x, float y, float w, float h)
    {
        // GeometryGuard.ValidateFloats(x, y, w, h);
        this.Left = x;
        this.Top = y;
        this.Width = w;
        this.Height = h;
    }

    // FAST path: no Regex allocation, no Match boxing, minimal parsing.
    [ArgReviver]
    public static RectF Revive(string key, string value)
    {
        // Accept the same comma-separated format. We allow optional whitespace around parts.
        ReadOnlySpan<char> s = value.AsSpan().Trim();
        int c1 = s.IndexOf(',');
        if (c1 <= 0) throw new ValidationArgException($"Invalid RectF: " + value);
        int c2 = s.Slice(c1 + 1).IndexOf(',');
        if (c2 < 0) throw new ValidationArgException($"Invalid RectF: " + value);
        c2 += c1 + 1;
        int c3 = s.Slice(c2 + 1).IndexOf(',');
        if (c3 < 0) throw new ValidationArgException($"Invalid RectF: " + value);
        c3 += c2 + 1;

        var l = ParseFloat(s.Slice(0, c1));
        var t = ParseFloat(s.Slice(c1 + 1, c2 - (c1 + 1)));
        var w = ParseFloat(s.Slice(c2 + 1, c3 - (c2 + 1)));
        var h = ParseFloat(s.Slice(c3 + 1));

        return new RectF(l, t, w, h);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float ParseFloat(ReadOnlySpan<char> part)
        {
            part = part.Trim();
            if (!float.TryParse(part, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var f))
                throw new ValidationArgException($"Invalid RectF component: {part.ToString()}");
            return f;
        }
    }

    public override string ToString() => $"{Left},{Top} {Width}x{Height}";
    public bool Equals(in Rect other) => Equals(other.ToRectF());
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(in RectF other) => Left == other.Left && Top == other.Top && Width == other.Width && Height == other.Height;
    public bool Equals(RectF other) => Left == other.Left && Top == other.Top && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is RectF && Equals((RectF)obj);
    public static bool operator ==(in RectF a, in RectF b) => a.Equals(b);
    public static bool operator !=(in RectF a, in RectF b) => a.Equals(b) == false;

    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            hash = hash * 23 + Left.GetHashCode();
            hash = hash * 23 + Top.GetHashCode();
            hash = hash * 23 + Width.GetHashCode();
            hash = hash * 23 + Height.GetHashCode();
            return hash;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RectF Offset(float dx, float dy) => Offset(Left, Top, Width, Height, dx, dy);

    public RectF RadialOffset(Angle angle, float distance, bool normalized = true) =>
        RadialOffset(Left, Top, Width, Height, angle, distance, normalized);

    public RectF Round() => new RectF(ConsoleMath.Round(Left), ConsoleMath.Round(Top), ConsoleMath.Round(Width), ConsoleMath.Round(Height));
    public Rect ToRect() => new Rect(ConsoleMath.Round(Left), ConsoleMath.Round(Top), ConsoleMath.Round(Width), ConsoleMath.Round(Height));

    public RectF ToCenterdAt(LocF loc)
    {
        var x = loc.Left - (Width * 0.5f);
        var y = loc.Top - (Height * 0.5f);
        return new RectF(x, y, Width, Height);
    }

    public RectF Grow(float percentage)
    {
        var center = Center;
        var newW = Width * (1 + percentage);
        var newH = Height * (1 + percentage);
        return new RectF(center.Left - newW * 0.5f, center.Top - newH * 0.5f, newW, newH);
    }

    public RectF Grow(float newWidth, float newHeight)
    {
        var center = Center;
        return new RectF(center.Left - newWidth * 0.5f, center.Top - newHeight * 0.5f, newWidth, newHeight);
    }

    public RectF Shrink(float percentage)
    {
        var center = Center;
        var newW = Width * (1 - percentage);
        var newH = Height * (1 - percentage);
        return new RectF(center.Left - newW * 0.5f, center.Top - newH * 0.5f, newW, newH);
    }

    public RectF ShrinkBy(float dx, float dy)
    {
        var center = Center;
        var newW = Width - dx;
        var newH = Height - dy;
        return new RectF(center.Left - newW * 0.5f, center.Top - newH * 0.5f, newW, newH);
    }

    public LocF GetTopLeftIfCenteredAt(float x, float y)
    {
        var left = x - Width * 0.5f;
        var top = y - Height * 0.5f;
        return new LocF(left, top);
    }

    public Angle CalculateAngleTo(in RectF other) => CalculateAngleTo(this, other);
    public Angle CalculateAngleTo(float bx, float by, float bw, float bh) => CalculateAngleTo(Left, Top, Width, Height, bx, by, bw, bh);

    public float CalculateDistanceTo(in RectF other) => CalculateDistanceTo(this, other);
    public float CalculateDistanceTo(float bx, float by, float bw, float bh) => CalculateDistanceTo(Left, Top, Width, Height, bx, by, bw, bh);
    public float CalculateNormalizedDistanceTo(in RectF other) => CalculateNormalizedDistanceTo(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    public float CalculateNormalizedDistanceTo(float bx, float by, float bw, float bh) => CalculateNormalizedDistanceTo(Left, Top, Width, Height, bx, by, bw, bh);

    public static Angle CalculateAngleTo(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
    {
        var aCenterX = ax + (aw * 0.5f);
        var aCenterY = ay + (ah * 0.5f);

        var bCenterX = bx + (bw * 0.5f);
        var bCenterY = by + (bh * 0.5f);
        return LocF.CalculateAngleTo(aCenterX, aCenterY, bCenterX, bCenterY);
    }

    public static Angle CalculateAngleTo(in RectF a, in RectF b)
    {
        var aCenterX = a.Left + (a.Width * 0.5f);
        var aCenterY = a.Top + (a.Height * 0.5f);

        var bCenterX = b.Left + (b.Width * 0.5f);
        var bCenterY = b.Top + (b.Height * 0.5f);
        return LocF.CalculateAngleTo(aCenterX, aCenterY, bCenterX, bCenterY);
    }

    public static float CalculateNormalizedDistanceTo(in RectF a, in RectF b)
    {
       return CalculateNormalizedDistanceTo(a.Left, a.Top, a.Width, a.Height, b.Left, b.Top, b.Width, b.Height);
    }

    public static float CalculateNormalizedDistanceTo(
        float ax, float ay, float aw, float ah,
        float bx, float by, float bw, float bh,
        float aspectRatio = 2.0f)
    {
        float ax2 = ax + aw;
        float ay2 = ay + ah;
        float bx2 = bx + bw;
        float by2 = by + bh;

        float dx = MathF.Max(ax - bx2, bx - ax2);
        float dy = MathF.Max(ay - by2, by - ay2);

        dx = MathF.Max(dx, 0f);
        dy = MathF.Max(dy, 0f);

        // squash y before Euclidean
        dy *= aspectRatio;

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Branchless rectangle distance (same formulation as the in-RectF overload)
    public static float CalculateDistanceTo(in RectF a, in RectF b)
    {
        float dx = MathF.Max(a.Left - (b.Left + b.Width), b.Left - (a.Left + a.Width));
        float dy = MathF.Max(a.Top - (b.Top + b.Height), b.Top - (a.Top + a.Height));

        dx = MathF.Max(dx, 0);
        dy = MathF.Max(dy, 0);

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Replace branchy corner/edge cases with branchless formulation to match the other overload.
    public static float CalculateDistanceTo(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
    {
        float ax2 = ax + aw;
        float ay2 = ay + ah;
        float bx2 = bx + bw;
        float by2 = by + bh;

        float dx = MathF.Max(ax - bx2, bx - ax2);
        float dy = MathF.Max(ay - by2, by - ay2);

        dx = MathF.Max(dx, 0f);
        dy = MathF.Max(dy, 0f);

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NumberOfPixelsThatOverlap(RectF other) => NumberOfPixelsThatOverlap(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NumberOfPixelsThatOverlap(float x2, float y2, float w2, float h2) => NumberOfPixelsThatOverlap(Left, Top, Width, Height, x2, y2, w2, h2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float OverlapPercentage(RectF other) => OverlapPercentage(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float OverlapPercentage(float x2, float y2, float w2, float h2) => OverlapPercentage(Left, Top, Width, Height, x2, y2, w2, h2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Touches(RectF other) => Touches(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Touches(float x2, float y2, float w2, float h2) => Touches(Left, Top, Width, Height, x2, y2, w2, h2);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(RectF other) => Contains(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);

    public bool Contains(LocF other) => Contains(other.ToRect(.0001f, .0001f));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(float x2, float y2, float w2, float h2) => Contains(Left, Top, Width, Height, x2, y2, w2, h2);

    public static bool Contains(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2) =>
        OverlapPercentage(x1, y1, w1, h1, x2, y2, w2, h2) == 1f;

    public static bool Touches(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2) =>
        NumberOfPixelsThatOverlap(x1, y1, w1, h1, x2, y2, w2, h2) > 0f;

    public static float NumberOfPixelsThatOverlap(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        float r1 = x1 + w1;
        float r2 = x2 + w2;
        float b1 = y1 + h1;
        float b2 = y2 + h2;

        float a = MathF.Max(0f, MathF.Min(r1, r2) - MathF.Max(x1, x2));
        if (a == 0f) return 0f;
        float b = MathF.Max(0f, MathF.Min(b1, b2) - MathF.Max(y1, y2));
        return a * b;
    }

    public static float OverlapPercentage(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        float numerator = NumberOfPixelsThatOverlap(x1, y1, w1, h1, x2, y2, w2, h2);
        if (numerator <= 0f) return 0f;

        float denominator = w2 * h2;
        if (numerator == denominator) return 1f;

        // amount = clamp(numerator/denominator, 0..1), with a cheap snap-to-1 epsilon like original
        float amount = numerator / denominator;
        amount = MathF.Min(1f, MathF.Max(0f, amount));
        if (amount > 0.999f) amount = 1f;
        return amount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAbove(RectF other) => Top < other.Top;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBelow(RectF other) => Bottom > other.Bottom;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLeftOf(RectF other) => Left < other.Left;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRightOf(RectF other) => Right > other.Right;

    public static RectF RadialOffset(float x, float y, float w, float h, Angle angle, float distance, bool normalized = true)
    {
        var newLoc = LocF.RadialOffset(x, y, angle, distance, normalized);
        return new RectF(newLoc.Left, newLoc.Top, w, h);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RectF Offset(float x, float y, float w, float h, float dx, float dy) => new RectF(x + dx, y + dy, w, h);

    public RectF ToSameWithWiggleRoom() => new RectF(Left + .1f, Top + .1f, Width - .2f, Height - .2f);

    // NOTE: This allocates an array by contract; keeping the API intact.
    public LocF[] Corners => new LocF[]
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    };

    public RectF SweptAABB(in RectF after)
    {
        float left = MathF.Min(this.Left, after.Left);
        float top = MathF.Min(this.Top, after.Top);
        float right = MathF.Max(this.Right, after.Right);
        float bottom = MathF.Max(this.Bottom, after.Bottom);
        return new RectF(left, top, right - left, bottom - top);
    }

    public static RectF FromMass(ConsoleControl c, IEnumerable<ConsoleControl> others)
    {
        var left = c.Left;
        var top = c.Top;
        var right = c.Right();
        var bottom = c.Bottom();

        foreach (var child in others)
        {
            left = MathF.Min(left, child.Left);
            top = MathF.Min(top, child.Top);
            right = MathF.Max(right, child.Right());
            bottom = MathF.Max(bottom, child.Bottom());
        }

        var bounds = new RectF(left, top, right - left, bottom - top);
        return bounds;
    }

    public RectF ClipTo(in RectF other)
    {
        // Determine the maximum left/top and minimum right/bottom edges
        var x1 = MathF.Max(this.Left, other.Left);
        var y1 = MathF.Max(this.Top, other.Top);
        var x2 = MathF.Min(this.Left + this.Width, other.Left + other.Width);
        var y2 = MathF.Min(this.Top + this.Height, other.Top + other.Height);

        // If there’s no intersection, return an empty rect
        if (x2 <= x1 || y2 <= y1)
        {
            return new RectF(0, 0, 0, 0);
        }

        // Return the intersection rectangle
        return new RectF(x1, y1, x2 - x1, y2 - y1);
    }
}