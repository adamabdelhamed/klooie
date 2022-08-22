namespace klooie;

public readonly struct LocF
{
    public readonly float Left;
    public readonly float Top;

    public LocF(float x, float y)
    {
        this.Left = x;
        this.Top = y;
    }

    public RectF ToRect(float w, float h) => new RectF(Left - (w / 2), Top - (h / 2), w, h);

    public override string ToString() => $"{Left},{Top}";
    public LocF GetRounded() => new LocF(ConsoleMath.Round(Left), ConsoleMath.Round(Top));
    public LocF GetFloor() => new LocF((int)Left, (int)Top);
    public bool Equals(in LocF other) => Left == other.Left && Top == other.Top;
    public override bool Equals(object? obj) => obj is LocF && Equals((LocF)obj);
    public static bool operator ==(in LocF a, in LocF b) => a.Equals(b);
    public static bool operator !=(in LocF a, in LocF b) => a.Equals(b) == false;

    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            int hash = 17;
            hash = hash * 23 + Left.GetHashCode();
            hash = hash * 23 + Top.GetHashCode();
            return hash;
        }
    }

    public Angle CalculateAngleTo(float x2, float y2) => CalculateAngleTo(Left, Top, x2, y2);
    public Angle CalculateAngleTo(LocF other) => CalculateAngleTo(this, other);

    public float CalculateDistanceTo(float x2, float y2) => CalculateDistanceTo(Left, Top, x2, y2);
    public float CalculateDistanceTo(LocF other) => CalculateDistanceTo(this, other);

    public float CalculateNormalizedDistanceTo(float x2, float y2) => CalculateNormalizedDistanceTo(Left, Top, x2, y2);
    public float CalculateNormalizedDistanceTo(LocF other) => CalculateNormalizedDistanceTo(this, other);

    public LocF Offset(float dx, float dy) => new LocF(Left + dx, Top + dy);

    public static LocF Offset(float x, float y, float dx, float dy) => new LocF(x + dx, y + dy);

    public static LocF OffsetByAngleAndDistance(float x, float y, Angle angle, float distance, bool normalized = true)
    {
        if (normalized)
        {
            distance = ConsoleMath.NormalizeQuantity(distance, angle.Value);
        }
        var forward = angle.Value > 270 || angle.Value < 90;
        var up = angle.Value > 180;

        // convert to radians
        angle = (float)(angle.Value * Math.PI / 180);
        float dy = (float)Math.Abs(distance * Math.Sin(angle.Value));
        float dx = (float)Math.Sqrt((distance * distance) - (dy * dy));

        float x2 = forward ? x + dx : x - dx;
        float y2 = up ? y - dy : y + dy;

        return new LocF(x2, y2);
    }

    public LocF OffsetByAngleAndDistance(Angle angle, float distance, bool normalized = true)
    {
        if (normalized)
        {
            distance = ConsoleMath.NormalizeQuantity(distance, angle.Value);
        }
        var forward = angle.Value > 270 || angle.Value < 90;
        var up = angle.Value > 180;

        // convert to radians
        angle = (float)(angle.Value * Math.PI / 180);
        float dy = (float)Math.Abs(distance * Math.Sin(angle.Value));
        float dx = (float)Math.Sqrt((distance * distance) - (dy * dy));

        float x2 = forward ? Left + dx : Left - dx;
        float y2 = up ? Top - dy : Top + dy;

        return new LocF(x2, y2);
    }

    public static float CalculateNormalizedDistanceTo(in LocF a, in LocF b)
    {
        var d = CalculateDistanceTo(a, b);
        var angle = CalculateAngleTo(a, b);
        return ConsoleMath.NormalizeQuantity(d, angle.Value, true);
    }

    public static float CalculateNormalizedDistanceTo(float ax, float ay, float bx, float by)
    {
        var d = CalculateDistanceTo(ax, ay, bx, by);
        var a = CalculateAngleTo(ax, ay, bx, by);
        return ConsoleMath.NormalizeQuantity(d, a.Value, true);
    }

    public static Angle CalculateAngleTo(in LocF a, in LocF b)
    {
        float dx = b.Left - a.Left;
        float dy = b.Top - a.Top;
        float d = a.CalculateDistanceTo(b);

        if (dy == 0 && dx > 0) return 0;
        else if (dy == 0) return 180;
        else if (dx == 0 && dy > 0) return 90;
        else if (dx == 0) return 270;

        double radians, increment;
        if (dx >= 0 && dy >= 0)
        {
            // Sin(a) = dy / d
            radians = Math.Asin(dy / d);
            increment = 0;

        }
        else if (dx < 0 && dy > 0)
        {
            // Sin(a) = dx / d
            radians = Math.Asin(-dx / d);
            increment = 90;
        }
        else if (dy < 0 && dx < 0)
        {
            radians = Math.Asin(-dy / d);
            increment = 180;
        }
        else if (dx > 0 && dy < 0)
        {
            radians = Math.Asin(dx / d);
            increment = 270;
        }
        else
        {
            throw new Exception($"Failed to calculate angle from {a.Left},{a.Top} to {b.Left},{b.Top}");
        }

        var ret = (float)(increment + radians * 180 / Math.PI);

        if (ret == 360) ret = 0;

        return ret;
    }

    public static Angle CalculateAngleTo(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float d = CalculateDistanceTo(x1, y1, x2, y2);

        if (dy == 0 && dx > 0) return 0;
        else if (dy == 0) return 180;
        else if (dx == 0 && dy > 0) return 90;
        else if (dx == 0) return 270;

        double radians, increment;
        if (dx >= 0 && dy >= 0)
        {
            // Sin(a) = dy / d
            radians = Math.Asin(dy / d);
            increment = 0;

        }
        else if (dx < 0 && dy > 0)
        {
            // Sin(a) = dx / d
            radians = Math.Asin(-dx / d);
            increment = 90;
        }
        else if (dy < 0 && dx < 0)
        {
            radians = Math.Asin(-dy / d);
            increment = 180;
        }
        else if (dx > 0 && dy < 0)
        {
            radians = Math.Asin(dx / d);
            increment = 270;
        }
        else
        {
            throw new Exception($"Failed to calculate angle from {x1},{y1} to {x2},{y2}");
        }

        var ret = (float)(increment + radians * 180 / Math.PI);

        if (ret == 360) ret = 0;

        return ret;
    }

    public static float CalculateDistanceTo(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static float CalculateDistanceTo(in LocF a, in LocF b)
    {
        var dx = a.Left - b.Left;
        var dy = a.Top - b.Top;
        return (float)Math.Sqrt((dx * dx) + (dy * dy));
    }
}
