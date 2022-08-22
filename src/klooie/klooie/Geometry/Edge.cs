namespace klooie;

public readonly struct Edge
{
    public readonly float X1;
    public readonly float Y1;

    public readonly float X2;
    public readonly float Y2;

    public Edge()
    {
        X1 = default;
        Y1 = default;
        X2 = default;
        Y2 = default;
    }

    public Edge(float x1, float y1, float x2, float y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public override string ToString() => $"{X1},{Y1} => {X2},{Y2}";
    public bool Equals(Edge other) => X1 == other.X1 && X2 == other.X2 && Y1 == other.Y1 && Y2 == other.Y2;
    public override bool Equals(object? obj) => obj is Edge && Equals((Edge)obj);
    public static bool operator ==(Edge a, Edge b) => a.Equals(b);
    public static bool operator !=(Edge a, Edge b) => a.Equals(b) == false;
    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            hash = hash * 23 + X1.GetHashCode();
            hash = hash * 23 + X2.GetHashCode();
            hash = hash * 23 + Y1.GetHashCode();
            hash = hash * 23 + Y2.GetHashCode();
            return hash;
        }
    }
}