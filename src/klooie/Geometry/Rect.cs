namespace klooie;
public readonly struct Rect
{
    public readonly int Left;
    public readonly int Top;
    public readonly int Width;
    public readonly int Height;

    public int Right => Left + Width;
    public int Bottom => Top + Height;

    public int CenterX => Left + Width / 2;
    public int CenterY => Top + Height / 2;
    public LocF Center => new LocF(CenterX, CenterY);
    public Loc TopLeft => new Loc(Left, Top);
    public Loc TopRight => new Loc(Right, Top);
    public Loc BottomLeft => new Loc(Left, Bottom);
    public Loc BottomRight => new Loc(Right, Bottom);

    public float Hypotenous => MathF.Sqrt(Width * Width + Height * Height);

    public Rect(int x, int y, int w, int h)
    {
        this.Left = x;
        this.Top = y;
        this.Width = w;
        this.Height = h;
    }

    public RectF ToRectF() => new RectF(Left, Top, Width, Height);

    public override string ToString() => $"{Left},{Top} {Width}x{Height}";
    public bool Equals(in RectF other) => ToRectF().Equals(other);
    public bool Equals(in Rect other) => Left == other.Left && Top == other.Top && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => (obj is Rect && Equals((Rect)obj)) || (obj is RectF && Equals((RectF)obj));
    public static bool operator ==(in Rect a, in Rect b) => a.Equals(b);
    public static bool operator !=(in Rect a, in Rect b) => a.Equals(b) == false;
    public static bool operator ==(in Rect a, in RectF b) => a.Equals(b);
    public static bool operator !=(in Rect a, in RectF b) => a.Equals(b) == false;
    public static bool operator ==(in RectF a, in Rect b) => a.Equals(b);
    public static bool operator !=(in RectF a, in Rect b) => a.Equals(b) == false;

    public override int GetHashCode() => ToRectF().GetHashCode();

    public Rect Offset(int dx, int dy) => new Rect(Left + dx, Top + dy, Width, Height);
    public Rect RadialOffset(Angle angle, float distance, bool normalized = true) => ToRectF().RadialOffset(angle, distance, normalized).ToRect();
    public Rect Grow(float percentage) => ToRectF().Grow(percentage).ToRect();
    public Rect Shrink(float percentage) => ToRectF().Shrink(percentage).ToRect();
    public Angle CalculateAngleTo(in Rect other) => ToRectF().CalculateAngleTo(other.ToRectF());
    public Angle CalculateAngleTo(float bx, float by, float bw, float bh) => ToRectF().CalculateAngleTo(bx, by, bw, bh);
    public float CalculateDistanceTo(in Rect other) => ToRectF().CalculateDistanceTo(other.ToRectF());
    public float CalculateDistanceTo(int bx, int by, int bw, int bh) => ToRectF().CalculateDistanceTo(bx, by, bw, bh);
    public float CalculateNormalizedDistanceTo(in Rect other) => ToRectF().CalculateNormalizedDistanceTo(other.ToRectF());
    public int NumberOfPixelsThatOverlap(Rect other) => (int)ToRectF().NumberOfPixelsThatOverlap(other.ToRectF());
    public float NumberOfPixelsThatOverlap(int x2, int y2, int w2, int h2) => (int)ToRectF().NumberOfPixelsThatOverlap(x2, y2, w2, h2);
    public float OverlapPercentage(Rect other) => ToRectF().OverlapPercentage(other.ToRectF());
    public float OverlapPercentage(int x2, int y2, int w2, int h2) => ToRectF().OverlapPercentage(x2, y2, w2, h2);
    public bool Touches(Rect other) => ToRectF().Touches(other.ToRectF());
    public bool Touches(int x2, int y2, int w2, int h2) => ToRectF().Touches(x2, y2, w2, h2);
    public bool Contains(Rect other) => ToRectF().Contains(other.ToRectF());
    public bool Contains(int x2, int y2, int w2, int h2) => ToRectF().Contains(x2, y2, w2, h2);
    public static bool Contains(int x1, int y1, int w1, int h1, int x2, int y2, int w2, int h2) => RectF.OverlapPercentage(x1, y1, w1, h1, x2, y2, w2, h2) == 1;
    public static bool Touches(int x1, int y1, int w1, int h1, int x2, int y2, int w2, int h2) => RectF.NumberOfPixelsThatOverlap(x1, y1, w1, h1, x2, y2, w2, h2) > 0;
    public static float NumberOfPixelsThatOverlap(int x1, int y1, int w1, int h1, int x2, int y2, int w2, int h2) => RectF.NumberOfPixelsThatOverlap(x1, y1, w1, h1, x2, y2, w2, h2);
    public static float OverlapPercentage(int x1, int y1, int w1, int h1, int x2, int y2, int w2, int h2) => RectF.OverlapPercentage(x1, y1, w1, h1, x2, y2, w2, h2);

    public bool IsAbove(Rect other) => Top < other.Top;
    public bool IsBelow(Rect other) => Bottom > other.Bottom;
    public bool IsLeftOf(Rect other) => Left < other.Left;
    public bool IsRightOf(Rect other) => Right > other.Right;
}