namespace klooie;

/// <summary>
/// A type that represents a circle and has geometric properties that are
/// useful when working with circles. Full disclosure this is not fully built
/// out, but what is built out works.
/// </summary>
public readonly struct Circle
{
    public readonly float CX;
    public readonly float CY;
    public readonly float Radius;

    /// <summary>
    /// Default constructor for a circle
    /// </summary>
    public Circle()
    {
        CX = default;
        CY = default;
        Radius = default;
    }

    /// <summary>
    /// Creates a circle given a center point and a non-negative radius
    /// </summary>
    /// <param name="cx">the x coordinate of the center</param>
    /// <param name="cy">the y coordinate of the center</param>
    /// <param name="radius">the radius of the circle</param>
    /// <exception cref="ArgumentException">if a negative radius is given</exception>
    public Circle(float cx, float cy, float radius)
    {
        if (radius < 0) throw new ArgumentException("A circle's radius cannot be negative");
        CX = cx;
        CY = cy;
        Radius = radius;
    }

    /// <summary>
    /// Checks to see if these two circles are equal
    /// </summary>
    /// <param name="other">the other circle</param>
    /// <returns>true if equal, false otherwise</returns>
    public bool Equals(Circle other) => CX == other.CX && CY == other.CY && Radius == other.Radius;

    /// <summary>
    /// Checks to see if these two circles are equal
    /// </summary>
    /// <param name="other">the other circle</param>
    /// <returns>true if equal, false otherwise</returns>
    public override bool Equals(object? obj) => obj is Circle && Equals((Circle)obj);

    /// <summary>
    /// overload for ==
    /// </summary>
    /// <param name="a">a circle</param>
    /// <param name="b">another circle</param>
    /// <returns>true if equal, false otherwise</returns>
    public static bool operator ==(Circle a, Circle b) => a.Equals(b);

    /// <summary>
    /// overload for !=
    /// </summary>
    /// <param name="a">a circle</param>
    /// <param name="b">another circle</param>
    /// <returns>true if not equal, false otherwise</returns>
    public static bool operator !=(Circle a, Circle b) => a.Equals(b) == false;

    /// <summary>
    /// Gets a hashcode that is based on the properties of the circle (cx,cy,radius)
    /// </summary>
    /// <returns>a hashcode that is based on the properties of the circle (cx,cy,radius)</returns>
    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            hash = hash * 23 + CX.GetHashCode();
            hash = hash * 23 + CY.GetHashCode();
            hash = hash * 23 + Radius.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// gets a string representation of the circle
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"Center: {CX},{CY}, Radius: {Radius}";

    /// <summary>
    /// Finds the intersections between this circle and a line. WARNING - Even though
    /// this takes in 2 points they are treated as a line, not a line segment. So if
    /// you were expecting a line segment that enters the circle, but does not exit it
    /// to return 1 location then you will be disappointed when this method treats your segment
    /// as a line, which does exit the circle, and returns 2 locations.
    /// </summary>
    /// <param name="e">the line to test, as defined by 2 example points</param>
    /// <returns>a set of locations where the line intersects the circle</returns>
    public IEnumerable<LocF> FindLineCircleIntersections(Edge e) => FindLineCircleIntersections(CX, CY, Radius, e);

    /// <summary>
    /// Finds the intersections between a circle and a line. WARNING - Even though
    /// this takes in 2 points they are treated as a line, not a line segment. So if
    /// you were expecting a line segment that enters the circle, but does not exit it
    /// to return 1 location then you will be disappointed when this method treats your segment
    /// as a line, which does exit the circle, and returns 2 locations.
    /// </summary>
    /// <param name="cx">the center x coordinate of the circle</param>
    /// <param name="cy">the center y coordinate of the circle</param>
    /// <param name="radius">the radius of the circle</param>
    /// <param name="e">the line to test, as defined by 2 example points</param>
    /// <returns>a set of locations where the line intersects the circle</returns>
    public static IEnumerable<LocF> FindLineCircleIntersections(float cx, float cy, float radius, Edge e)
    {
        var ret = FindLineCircleIntersections(cx, cy, radius, e.X1, e.Y1, e.X2, e.Y2, out float ox1, out float oy1, out float ox2, out float oy2);
        if (ret == 0) return Enumerable.Empty<LocF>();
        else if (ret == 1) return new LocF[] { new LocF(ox1, oy1) };
        else return new LocF[] { new LocF(ox1, oy1), new LocF(ox2, oy2) };
    }


    /// <summary>
    /// Finds the intersections between a circle and a line. WARNING - Even though
    /// this takes in 2 points they are treated as a line, not a line segment. So if
    /// you were expecting a line segment that enters the circle, but does not exit it
    /// to return 1 then you will be disappointed when this method treats your segment
    /// as a line, which does exit the circle, and returns 2.
    /// </summary>
    /// <param name="cx">the center x coordinate of the circle</param>
    /// <param name="cy">the center y coordinate of the circle</param>
    /// <param name="radius">the radius of the circle</param>
    /// <param name="x1">the x coordinate of the line's first point</param>
    /// <param name="y1">the y coordinate of the line's first point</param>
    /// <param name="x2">the x coordinate of the line's second point</param>
    /// <param name="y2">the y coordinate of the line's second point</param>
    /// <param name="ox1">the x coordinate of the first intersection point, if found</param>
    /// <param name="oy1">the y coordinate of the first intersection point, if found</param>
    /// <param name="ox2">the x coordinate of the second intersection point, if found</param>
    /// <param name="oy2">the y coordinate of the second intersection point, if found</param>
    /// <returns>the number of intersections found which can be 0, 1, or 2</returns>
    public static int FindLineCircleIntersections(float cx, float cy, float radius, float x1, float y1, float x2, float y2, out float ox1, out float oy1, out float ox2, out float oy2)
    {
        float dx, dy, A, B, C, det, t;

        dx = x2 - x1;
        dy = y2 - y1;

        A = dx * dx + dy * dy;
        B = 2 * (dx * (x1 - cx) + dy * (y1 - cy));
        C = (x1 - cx) * (x1 - cx) +
            (y1 - cy) * (y1 - cy) -
            radius * radius;

        det = B * B - 4 * A * C;
        if ((A <= 0.0000001) || (det < 0))
        {
            // No real solutions.
            ox1 = float.NaN;
            ox2 = float.NaN;
            oy1 = float.NaN;
            oy2 = float.NaN;
            return 0;
        }
        else if (det == 0)
        {
            // One solution.
            t = -B / (2 * A);

            ox1 = x1 + t * dx;
            oy1 = y1 + t * dy;

            ox2 = float.NaN;
            oy2 = float.NaN;
            return 1;
        }
        else
        {
            // Two solutions.
            t = (float)((-B + Math.Sqrt(det)) / (2 * A));

            ox1 = x1 + t * dx;
            oy1 = y1 + t * dy;

            t = (float)((-B - Math.Sqrt(det)) / (2 * A));

            ox2 = x1 + t * dx;
            oy2 = y1 + t * dy;
            return 2;
        }
    }
}
