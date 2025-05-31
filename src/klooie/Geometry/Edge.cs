namespace klooie;

public readonly struct Edge
{
    public readonly float X1;
    public readonly float Y1;

    public readonly float X2;
    public readonly float Y2;

    public LocF From => new LocF(X1, Y1);
    public LocF To => new LocF(X2, Y2);

    public Edge()
    {
        X1 = default;
        Y1 = default;
        X2 = default;
        Y2 = default;
    }

    public Edge(float x1, float y1, float x2, float y2)
    {
        GeometryGuard.ValidateFloats(x1, y1, x2, y2);
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public float CalculateDistanceTo(float x, float y)
    {
        // Project the point (x, y) onto the edge and calculate the perpendicular distance
        float dx = X2 - X1;
        float dy = Y2 - Y1;

        float t = ((x - X1) * dx + (y - Y1) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1); // Clamp to edge segment

        float closestX = X1 + t * dx;
        float closestY = Y1 + t * dy;

        return LocF.CalculateDistanceTo(x, y, closestX, closestY);
    }

    public bool Contains(LocF point)
    {
        if (point == From || point == To) return true;
        var raySlope = From.CalculateAngleTo(To);
        var testSlope = From.CalculateAngleTo(point);
        return raySlope == testSlope;
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