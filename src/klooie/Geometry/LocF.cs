using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace klooie;

[ArgReviverType]
public readonly struct LocF
{
    public readonly float Left;
    public readonly float Top;

    // I made a terrible mistake naming these properties Left and Top instead of X and Y. Now LLMs always assume X and Y are present so I might as well add them as aliases.
    public float X => Left;
    public float Y => Top;

    public LocF(float x, float y)
    {
      //  GeometryGuard.ValidateFloats(x, y);
        this.Left = x;
        this.Top = y;
    }

    public RectF ToRect(float w, float h) => new RectF(Left - (w / 2), Top - (h / 2), w, h);

    public override string ToString() => $"{Left},{Top}";
    public LocF GetRounded() => new LocF(ConsoleMath.Round(Left), ConsoleMath.Round(Top));
    public Loc ToLoc() => new Loc(ConsoleMath.Round(Left), ConsoleMath.Round(Top));
    public LocF GetFloor() => new LocF((int)Left, (int)Top);
    public LocF GetCeiling() => new LocF(MathF.Ceiling(Left), MathF.Ceiling(Top));
    public bool Equals(in Loc other) => Left == other.Left && Top == other.Top;
    public bool Equals(in LocF other) => Left == other.Left && Top == other.Top;
    public override bool Equals(object? obj) => (obj is LocF && Equals((LocF)obj)) || (obj is Loc && Equals((Loc)obj));
    public static bool operator ==(in LocF a, in LocF b) => a.Equals(b);
    public static bool operator !=(in LocF a, in LocF b) => a.Equals(b) == false;


    [ArgReviver]
    public static LocF Revive(string key, string value)
    {
        var regex = new Regex(@"(?<Left>-?\d+),(?<Top>-?\d+)");
        var match = regex.Match(value);
        if (match.Success == false) throw new ValidationArgException($"Invalid LocF: " + value);
        var parse = (Match m, string field) => float.Parse(m.Groups[field].Value);
        return new LocF(parse(match, "Left"), parse(match, "Top"));
    }

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
    private const float Deg2Rad = (MathF.PI / 180f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LocF RadialOffset(float x, float y, Angle angle, float distance, bool normalized = true)
    {
        if (normalized)
            distance = ConsoleMath.NormalizeQuantity(distance, angle.Value);

        float rad = angle.Value * Deg2Rad;

        // Your unsafe tuple SinCos(float x) -> (Sin, Cos)
        var (sin, cos) = MathF.SinCos(rad);

        float x2 = ConsoleMath.Round(x + distance * cos, 5);
        float y2 = ConsoleMath.Round(y + distance * sin, 5);

        return new LocF(x2, y2);
    }

    public LocF RadialOffset(Angle angle, float radius, float aspectRatio = 2.0f)
    {
        float rad = angle.Value * MathF.PI / 180f;
        float x2 = Left + radius * MathF.Cos(rad);
        float y2 = Top + radius * MathF.Sin(rad) / aspectRatio;
        return new LocF(x2, y2);
    }

    public static float CalculateNormalizedDistanceTo(in LocF a, in LocF b, float aspectRatio = 2.0f)
    {
        float dx = b.Left - a.Left;
        float dy = (b.Top - a.Top) * aspectRatio; 
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static float CalculateNormalizedDistanceTo(float ax, float ay, float bx, float by, float aspectRatio = 2.0f)
    {
        float dx = bx - ax;
        float dy = (by - ay) * aspectRatio; 

        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static Angle CalculateAngleTo(in LocF a, in LocF b)
    {
        float dx = b.Left - a.Left;
        float dy = b.Top - a.Top;
        float d = a.CalculateDistanceTo(b);

        if (dy == 0 && dx > 0) return 0f;
        else if (dy == 0) return 180f;
        else if (dx == 0 && dy > 0) return 90f;
        else if (dx == 0) return 270f;

        float radians, increment;
        if (dx >= 0 && dy >= 0)
        {
            // Sin(a) = dy / d
            radians = MathF.Asin(dy / d);
            increment = 0f;

        }
        else if (dx < 0 && dy > 0)
        {
            // Sin(a) = dx / d
            radians = MathF.Asin(-dx / d);
            increment = 90f;
        }
        else if (dy < 0 && dx < 0)
        {
            radians = MathF.Asin(-dy / d);
            increment = 180f;
        }
        else if (dx > 0 && dy < 0)
        {
            radians = MathF.Asin(dx / d);
            increment = 270f;
        }
        else
        {
            throw new Exception($"Failed to calculate angle from {a.Left},{a.Top} to {b.Left},{b.Top}");
        }

        var ret = increment + radians * 180f / MathF.PI;

        if (ret == 360f) ret = 0f;

        return ret;
    }

    public static Angle CalculateAngleTo(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1; // x difference
        float dy = y2 - y1; // y difference (downward positive)

        float angleInRadians = MathF.Atan2(dy, dx);
        float angleInDegrees = angleInRadians * (180.0f / MathF.PI);

        if (angleInDegrees < 0)
        {
            angleInDegrees += 360.0f;
        }

        return angleInDegrees;
    }

    public static float CalculateDistanceTo(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    public static float CalculateDistanceTo(in LocF a, in LocF b)
    {
        var dx = a.Left - b.Left;
        var dy = a.Top - b.Top;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    public LocF Lerp(LocF target, float progress)
    {
        // Clamp progress to [0,1] to avoid overshoot
        if (progress < 0f) progress = 0f;
        else if (progress > 1f) progress = 1f;

        float newLeft = Left + (target.Left - Left) * progress;
        float newTop = Top + (target.Top - Top) * progress;

        return new LocF(newLeft, newTop);
    }
}
