namespace klooie;

/// <summary>
/// A type that represents an angle and has geometric properties that are
/// useful when working with angles.
/// </summary>
[ArgReviverType]
public readonly struct Angle
{
    /// <summary>
    /// points to the left
    /// </summary>
    public static readonly Angle Left = 180f;
    /// <summary>
    /// points upwards
    /// </summary>
    public static readonly Angle Up = 270f;
    /// <summary>
    /// points to the right
    /// </summary>
    public static readonly Angle Right = 0f;
    /// <summary>
    /// points downward
    /// </summary>
    public static readonly Angle Down = 90f;

    /// <summary>
    /// points up and to the left
    /// </summary>
    public static readonly Angle UpLeft = (Up.Value + Left.Value) / 2;
    /// <summary>
    /// points up and to the right
    /// </summary>
    public static readonly Angle UpRight = (Up.Value + 360) / 2;
    /// <summary>
    /// points down and to the right
    /// </summary>
    public static readonly Angle DownRight = (Down.Value + Right.Value) / 2;
    /// <summary>
    /// points down and to the left
    /// </summary>
    public static readonly Angle DownLeft = (Down.Value + Left.Value) / 2;

    /// <summary>
    /// the angle value as a float between 0 (inclusive) and 360 (exclusive)
    /// </summary>
    public readonly float Value;

    /// <summary>
    /// Creates a new angle given a float. The float given can be any real number
    /// and will be converted to a value between 0 (inclusive) and 360 (exclusive)
    /// </summary>
    /// <param name="val">the value of the angle</param>
    public Angle(float val)
    {
        Value = val % 360f;
        if(Value < 0)
        {
            Value = 360 + Value;
        }

        // handle strange -0 case
        if(float.IsNegative(Value))
        {
            Value = 0;
        }
    }

    /// <summary>
    /// prints the value in degrees
    /// </summary>
    /// <returns>string representation of the angle</returns>
    public override string ToString() => $"{Value} degrees";

    /// <summary>
    /// Will be true if the Value of the other angle equals this angle's value
    /// </summary>
    /// <param name="other">the other angle to compare with</param>
    /// <returns>true if the Value of the other angle equals this angle's value</returns>
    public bool Equals(Angle other) => Value == other.Value;

    /// <summary>
    /// Will be true if the Value of the other object is an angle and its value
    /// equals this angle's value
    /// </summary>
    /// <param name="obj">the other angle to compare with</param>
    /// <returns>true if the Value of the other angle equals this angle's value</returns>
    public override bool Equals(object? obj) => obj is Angle && Equals((Angle)obj);

    /// <summary>
    /// operator overload for equals
    /// </summary>
    /// <param name="a">first angle</param>
    /// <param name="b">second angle</param>
    /// <returns>true if the Value of the first angle equals the second angle's value</returns>
    public static bool operator ==(Angle a, Angle b) => a.Equals(b);

    /// <summary>
    /// operator overload for !=
    /// </summary>
    /// <param name="a">first angle</param>
    /// <param name="b">second angle</param>
    /// <returns>true if the Value of the first angle does not equal the second angle's value</returns>
    public static bool operator !=(Angle a, Angle b) => a.Equals(b) == false;

    /// <summary>
    /// Gets the hashcode of this angle
    /// </summary>
    /// <returns>the hashcode of this angle</returns>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Adds the given value to this angle and returns it as a new angle
    /// </summary>
    /// <param name="other">the angle value to add</param>
    /// <returns>a new angle that is the sum of this angle and the given parameter</returns>
    public Angle Add(float other)
    {
        var ret = Value + other;
        ret = ret % 360;
        ret = ret >= 0 ? ret : ret + 360;
        if (ret == 360) return new Angle(0);
        return new Angle(ret);
    }

    /// <summary>
    /// Adds the given value to this angle and returns it as a new angle
    /// </summary>
    /// <param name="other">the angle value to add</param>
    /// <returns>a new angle that is the sum of this angle and the given parameter</returns>
    public Angle Add(Angle other) => Add(other.Value);

    /// <summary>
    /// Gets the angle that is 180 degrees away from the current angle
    /// </summary>
    /// <returns>the angle that is 180 degrees away from the current angle</returns>
    public Angle Opposite() => Add(180);

    /// <summary>
    /// Uses the shortest path to get the # of degrees between this angle and another one.
    /// </summary>
    /// <param name="other">the other angle</param>
    /// <returns>the # of degrees between this angle and a given other angle</returns>
    public float DiffShortest(Angle other)
    {
        var c = Math.Abs(Value - other.Value);
        c = c <= 180 ? c : Math.Abs(360 - c);
        if (c == 360) return 0;
        return c;
    }

    /// <summary>
    /// Goes clockwise to measure the # of degrees between this angle and another one.
    /// </summary>
    /// <param name="other">the other angle</param>
    /// <returns>the # of degrees between this angle and a given other angle</returns>
    public float DiffClockwise(Angle other)
    {
        var diff = DiffShortest(other);
        var clock = Add(diff); // 1
        return clock == other ? diff : 360f - diff;
    }

    /// <summary>
    /// Goes counter-clockwise to measure the # of degrees between this angle and another one.
    /// </summary>
    /// <param name="other">the other angle</param>
    /// <returns>the # of degrees between this angle and a given other angle</returns>
    public float DiffCounterClockwise(Angle other)
    {
        var diff = DiffShortest(other);
        var clock = Add(diff);
        return clock == other ? 360f - diff : diff;
    }

    /// <summary>
    /// Gets a new angle that is the current angle rounded to the nearest angle.
    /// </summary>
    /// <param name="nearest">The amount to round to (e.g. 90) to round to the nearest NEWS</param>
    /// <returns>a new angle that is the current angle rounded to the nearest angle</returns>
    public Angle RoundAngleToNearest(Angle nearest) => new Angle(((float)ConsoleMath.Round(Value / nearest.Value) * nearest.Value) % 360);

    /// <summary>
    /// Determines if Clockwise is the shortest rotational path from this angle to another one
    /// </summary>
    /// <param name="other">the other angle</param>
    /// <returns>true if Clockwise is the shortest rotational path from this angle to another one</returns>
    public bool IsClockwiseShortestPathToAngle(Angle other) => Add(DiffShortest(other)) == other;

    /// <summary>
    /// Finds the angle that is between this angle and another angle
    /// </summary>
    /// <param name="to">the ending angle</param>
    /// <param name="clockwise">the direction to move when cutting the angle in half</param>
    /// <returns>the angle that is between these two angles</returns>
    public Angle Bisect(Angle to, bool clockwise)
    {
        var diff = clockwise ? DiffClockwise(to) : DiffCounterClockwise(to);
        var half = diff / 2f;
        var ret = clockwise ? this.Add(new Angle(half)) : this.Add(new Angle(-half));
        return ret;
    }

    /// <summary>
    /// A string that represents a text based arrow that maps to this angle
    /// </summary>
    public string ArrowString => "" + Arrow;

    /// <summary>
    /// A character that represents a text based arrow that maps to this angle
    /// </summary>
    public char Arrow
    {
        get
        {
            if (Value >= 315 || Value < 45)
            {
                return '>';
            }
            else if (Value >= 45 && Value < 135)
            {
                return 'v';
            }
            else if (Value >= 135 && Value < 225)
            {
                return '<';
            }
            else
            {
                return '^';
            }
        }
    }

    /// <summary>
    /// Returns true if the angle is 0,90,180, or 270
    /// </summary>
    public bool IsGridAligned => Value == Right || Value == Left || Value == Up || Value == Down;

    /// <summary>
    /// Converts an angle to radians
    /// </summary>
    /// <param name="degrees">the angle in degrees</param>
    /// <returns>the original angle, converted to radians</returns>
    public static float ToRadians(Angle degrees) => (float)(Math.PI * degrees.Value / 180.0);

    /// <summary>
    /// Converts radians to an Angle
    /// </summary>
    /// <param name="radians">a number in radians</param>
    /// <returns>An angle in degrees</returns>
    public static Angle ToDegrees(float radians) => new Angle((float)(radians * (180.0 / Math.PI)) % 360);

    /// <summary>
    /// Implicit definition for converting a float into an Angle
    /// </summary>
    /// <param name="a">the float to convert to an angle</param>
    public static implicit operator Angle(float a) => new Angle(a);

    /// <summary>
    /// An Arg Reviver that will allow PowerArgs to support Angle objects on the command line
    /// </summary>
    /// <param name="key">ignored</param>
    /// <param name="val">the command line parameter value that maps to an angle</param>
    /// <returns>a revived angle</returns>
    /// <exception cref="ValidationArgException">if the value could not be converted to an angle</exception>
    [ArgReviver]
    public static Angle Revive(string key, string val)
    {
        if (float.TryParse(val, out float f))
        {
            if (f < 0 || f >= 360) throw new ValidationArgException($"Angles must be >=0 and < 360, given: {val}");
            return new Angle(f);
        }
        else if (nameof(Up).Equals(val, StringComparison.OrdinalIgnoreCase))
        {
            return Up;
        }
        else if (nameof(Down).Equals(val, StringComparison.OrdinalIgnoreCase))
        {
            return Down;
        }
        else if (nameof(Left).Equals(val, StringComparison.OrdinalIgnoreCase))
        {
            return Left;
        }
        else if (nameof(Right).Equals(val, StringComparison.OrdinalIgnoreCase))
        {
            return Right;
        }
        else
        {
            throw new ValidationArgException($"Cannot parse angle: {val}");
        }
    }
}