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

    public float CenterX => Left + Width / 2;
    public float CenterY => Top + Height / 2;
    public LocF Center => new LocF(CenterX, CenterY);
    public LocF TopLeft => new LocF(Left, Top);
    public LocF TopRight => new LocF(Right, Top);
    public LocF BottomLeft => new LocF(Left, Bottom);
    public LocF BottomRight => new LocF(Right, Bottom);

    public Edge LeftEdge => new Edge(Left, Top, Left, Bottom);
    public Edge RightEdge => new Edge(Right, Top, Right, Bottom);
    public Edge TopEdge => new Edge(Left, Top, Right, Top);
    public Edge BottomEdge => new Edge(Left, Bottom, Right, Bottom);

    public float Hypotenous => (float)Math.Sqrt(Width * Width + Height * Height);

    public RectF Bounds => this;
    public bool CanCollideWith(ICollidable other) => true;

    public RectF(float x, float y, float w, float h)
    {
        GeometryGuard.ValidateFloats(x, y, w, h);
        this.Left = x;
        this.Top = y;
        this.Width = w;
        this.Height = h;
    }

    [ArgReviver]
    public static RectF Revive(string key, string value)
    {
        var regex = new Regex(@"(?<Left>-?\d+),(?<Top>-?\d+),(?<Width>-?\d+),(?<Height>-?\d+)");
        var match = regex.Match(value);
        if (match.Success == false) throw new ValidationArgException($"Invalid RectF: "+value);
        var parse = (Match m, string field) => float.Parse(m.Groups[field].Value);
        return new RectF(parse(match, "Left"), parse(match, "Top"), parse(match, "Width"), parse(match, "Height"));
    }

    public override string ToString() => $"{Left},{Top} {Width}x{Height}";
    public bool Equals(in Rect other) => Equals(other.ToRectF());
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

    public RectF Offset(float dx, float dy) => Offset(Left, Top, Width, Height, dx, dy);

    public RectF RadialOffset(Angle angle, float distance, bool normalized = true) =>
        RadialOffset(Left, Top, Width, Height, angle, distance, normalized);

    public RectF Round() => new RectF(ConsoleMath.Round(Left), ConsoleMath.Round(Top), ConsoleMath.Round(Width), ConsoleMath.Round(Height));
    public Rect ToRect() => new Rect(ConsoleMath.Round(Left), ConsoleMath.Round(Top), ConsoleMath.Round(Width), ConsoleMath.Round(Height));

    public RectF ToCenterdAt(LocF loc)
    {
        var x = loc.Left - (Width / 2f);
        var y = loc.Top - (Height / 2f);
        return new RectF(x, y, Width, Height);
    }

    public RectF Grow(float percentage)
    {
        var center = Center;
        var newW = Width * (1 + percentage);
        var newH = Height * (1 + percentage);
        return new RectF(center.Left - newW / 2f, center.Top - newH / 2f, newW, newH);
    }

    public RectF Grow(float newWidth, float newHeight)
    {
        var center = Center;
        return new RectF(center.Left - newWidth / 2f, center.Top - newHeight / 2f, newWidth, newHeight);
    }

    public RectF Shrink(float percentage)
    {
        var center = Center;
        var newW = Width * (1 - percentage);
        var newH = Height * (1 - percentage);
        return new RectF(center.Left - newW / 2f, center.Top - newH / 2f, newW, newH);
    }

    public RectF ShrinkBy(float dx, float dy)
    {
        var center = Center;
        var newW = Width - dx;
        var newH = Height - dy;
        return new RectF(center.Left - newW / 2f, center.Top - newH / 2f, newW, newH);
    }

    public LocF GetTopLeftIfCenteredAt(float x, float y)
    {
        var left = x - Width / 2f;
        var top = y - Height / 2f;
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
        var aCenterX = ax + (aw / 2);
        var aCenterY = ay + (ah / 2);

        var bCenterX = bx + (bw / 2);
        var bCenterY = by + (bh / 2);
        return LocF.CalculateAngleTo(aCenterX, aCenterY, bCenterX, bCenterY);
    }

    public static Angle CalculateAngleTo(in RectF a, in RectF b)
    {
        var aCenterX = a.Left + (a.Width / 2);
        var aCenterY = a.Top + (a.Height / 2);

        var bCenterX = b.Left + (b.Width / 2);
        var bCenterY = b.Top + (b.Height / 2);
        return LocF.CalculateAngleTo(aCenterX, aCenterY, bCenterX, bCenterY);
    }

    public static float CalculateNormalizedDistanceTo(in RectF a, in RectF b)
    {
        var d = CalculateDistanceTo(a, b);
        var angle = CalculateAngleTo(a, b);
        return ConsoleMath.NormalizeQuantity(d, angle, true);
    }

    public static float CalculateNormalizedDistanceTo(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
    {
        var d = CalculateDistanceTo(ax, ay, aw, ah, bx, by, bw, bh);
        var a = CalculateAngleTo(ax, ay, aw, ah, bx, by, bw, bh);
        return ConsoleMath.NormalizeQuantity(d, a, true);
    }

    public static float CalculateDistanceTo(in RectF a, in RectF b)
    {
        float dx = Math.Max(a.Left - (b.Left + b.Width), b.Left - (a.Left + a.Width));
        float dy = Math.Max(a.Top - (b.Top + b.Height), b.Top - (a.Top + a.Height));

        dx = Math.Max(dx, 0);
        dy = Math.Max(dy, 0);

        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static float CalculateDistanceTo(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
    {
        var ar = ax + aw;
        var ab = ay + ah;

        var br = bx + bw;
        var bb = by + bh;

        var left = br < ax;
        var right = ar < bx;
        var bottom = bb < ay;
        var top = ab < by;
        if (top && left)
            return LocF.CalculateDistanceTo(ax, ab, br, by);
        else if (left && bottom)
            return LocF.CalculateDistanceTo(ax, ay, br, bb);
        else if (bottom && right)
            return LocF.CalculateDistanceTo(ar, ay, bx, bb);
        else if (right && top)
            return LocF.CalculateDistanceTo(ar, ab, bx, by);
        else if (left)
            return ax - br;
        else if (right)
            return bx - ar;
        else if (bottom)
            return ay - bb;
        else if (top)
            return by - ab;
        else
            return 0;
    }

    public float NumberOfPixelsThatOverlap(RectF other) => NumberOfPixelsThatOverlap(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    public float NumberOfPixelsThatOverlap(float x2, float y2, float w2, float h2) => NumberOfPixelsThatOverlap(Left, Top, Width, Height, x2, y2, w2, h2);

    public float OverlapPercentage(RectF other) => OverlapPercentage(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    public float OverlapPercentage(float x2, float y2, float w2, float h2) => OverlapPercentage(Left, Top, Width, Height, x2, y2, w2, h2);

    public bool Touches(RectF other) => Touches(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    public bool Touches(float x2, float y2, float w2, float h2) => Touches(Left, Top, Width, Height, x2, y2, w2, h2);
    public bool Contains(RectF other) => Contains(Left, Top, Width, Height, other.Left, other.Top, other.Width, other.Height);
    public bool Contains(float x2, float y2, float w2, float h2) => Contains(Left, Top, Width, Height, x2, y2, w2, h2);

    public static bool Contains(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2) =>
        OverlapPercentage(x1, y1, w1, h1, x2, y2, w2, h2) == 1;

    public static bool Touches(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2) =>
        NumberOfPixelsThatOverlap(x1, y1, w1, h1, x2, y2, w2, h2) > 0;

    public static float NumberOfPixelsThatOverlap(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        var rectangleRight = x1 + w1;
        var otherRight = x2 + w2;
        var rectangleBottom = y1 + h1;
        var otherBottom = y2 + h2;
        var a = Math.Max(0, Math.Min(rectangleRight, otherRight) - Math.Max(x1, x2));
        if (a == 0) return 0;
        var b = Math.Max(0, Math.Min(rectangleBottom, otherBottom) - Math.Max(y1, y2));
        return a * b;
    }

    public static float OverlapPercentage(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        var numerator = NumberOfPixelsThatOverlap(x1, y1, w1, h1, x2, y2, w2, h2);
        var denominator = w2 * h2;

        if (numerator == 0) return 0;
        else if (numerator == denominator) return 1;

        var amount = numerator / denominator;
        if (amount < 0) amount = 0;
        else if (amount > 1) amount = 1;

        if (amount > .999)
        {
            amount = 1;
        }

        return amount;
    }

    public bool IsAbove(RectF other)
    {
        return Top < other.Top;
    }

    public bool IsBelow(RectF other)
    {
        return Bottom > other.Bottom;
    }

    public bool IsLeftOf(RectF other)
    {
        return Left < other.Left;
    }

    public bool IsRightOf(RectF other)
    {
        return Right > other.Right;
    }

    public static RectF RadialOffset(float x, float y, float w, float h, Angle angle, float distance, bool normalized = true)
    {
        var newLoc = LocF.RadialOffset(x, y, angle, distance, normalized);
        return new RectF(newLoc.Left, newLoc.Top, w, h);
    }

    public static RectF Offset(float x, float y, float w, float h, float dx, float dy) => new RectF(x + dx, y + dy, w, h);

    public RectF ToSameWithWiggleRoom() => new RectF(Left + .1f, Top + .1f, Width - .2f, Height - .2f);


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
            left = Math.Min(left, child.Left);
            top = Math.Min(top, child.Top);
            right = Math.Max(right, child.Right());
            bottom = Math.Max(bottom, child.Bottom());
        }

        var bounds = new RectF(left, top, right - left, bottom - top);
        return bounds;
    }
}