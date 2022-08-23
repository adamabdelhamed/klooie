namespace klooie;

public readonly struct Loc
{
    public readonly int Left;
    public readonly int Top;

    public Loc(int x, int y) { this.Left = x; this.Top = y; }
    public LocF ToLocF() => new LocF(Left,Top);
    public RectF ToRect(float w, float h) => new RectF(Left - (w / 2f), Top - (h / 2f), w, h);
    public override string ToString() => $"{Left},{Top}";
    public bool Equals(in Loc other) => Left == other.Left && Top == other.Top;
    public bool Equals(in LocF other) => Left == other.Left && Top == other.Top;
    public override bool Equals(object? obj) => (obj is Loc && Equals((Loc)obj)) || (obj is LocF && Equals((LocF)obj));
    public static bool operator ==(in Loc a, in Loc b) => a.Equals(b);
    public static bool operator !=(in Loc a, in Loc b) => a.Equals(b) == false;
    public override int GetHashCode() => ToLocF().GetHashCode();
    public Angle CalculateAngleTo(float x2, float y2) => ToLocF().CalculateAngleTo(x2, y2);
    public Angle CalculateAngleTo(Loc other) => ToLocF().CalculateAngleTo(other.Left, other.Top);
    public Angle CalculateAngleTo(LocF other) => ToLocF().CalculateAngleTo(other.Left, other.Top);
    public float CalculateDistanceTo(int x2, int y2) => ToLocF().CalculateDistanceTo(x2, y2);
    public float CalculateDistanceTo(float x2, float y2) => ToLocF().CalculateDistanceTo(x2, y2);
    public float CalculateDistanceTo(Loc other) => ToLocF().CalculateDistanceTo(other.Left, other.Top);
    public float CalculateDistanceTo(LocF other) => ToLocF().CalculateDistanceTo(other.Left, other.Top);
    public float CalculateNormalizedDistanceTo(int x2, int y2) => ToLocF().CalculateNormalizedDistanceTo(x2, y2);
    public float CalculateNormalizedDistanceTo(float x2, float y2) => ToLocF().CalculateNormalizedDistanceTo(x2, y2);
    public float CalculateNormalizedDistanceTo(Loc other) => ToLocF().CalculateNormalizedDistanceTo(other.Left, other.Top);
    public float CalculateNormalizedDistanceTo(LocF other) => ToLocF().CalculateNormalizedDistanceTo(other.Left, other.Top);
    public Loc Offset(int dx, int dy) => new Loc(Left + dx, Top + dy);
    
    public static Loc Offset(int x, int y, int dx, int dy) => new Loc(x + dx, y + dy);
    public static Loc OffsetByAngleAndDistance(float x, float y, Angle angle, float distance, bool normalized = true) => new LocF(x, y).OffsetByAngleAndDistance(angle, distance, normalized).ToLoc();
    public Loc OffsetByAngleAndDistance(Angle angle, float distance, bool normalized = true) => ToLocF().OffsetByAngleAndDistance(angle, distance, normalized).ToLoc();
    public static float CalculateNormalizedDistanceTo(in Loc a, in Loc b) => a.ToLocF().CalculateNormalizedDistanceTo(b.ToLocF());
    public static Angle CalculateAngleTo(in Loc a, in Loc b) => a.ToLocF().CalculateAngleTo(b.ToLocF());
    public static float CalculateDistanceTo(in Loc a, in Loc b) => a.ToLocF().CalculateDistanceTo(b.ToLocF());
}
