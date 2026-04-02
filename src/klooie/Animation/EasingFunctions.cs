namespace klooie;

public delegate float EasingFunction(float f);

public static class EasingFunctions
{
    /// <summary>
    /// A linear easing function
    /// </summary>
    /// <param name="percentage">the linear percentage</param>
    /// <returns>the linear percentage</returns>
    public static float Linear(float percentage) => percentage;

    /// <summary>
    /// An easing function that starts slow and accellerates as time moves on
    /// </summary>
    /// <param name="percentage">the linear percentage</param>
    /// <returns>the eased percentage</returns>
    public static float EaseIn(float percentage) => (float)Math.Pow(percentage, 5);

    /// <summary>
    /// An easing function that starts fast and decellerates as time moves on
    /// </summary>
    /// <param name="percentage">the linear percentage</param>
    /// <returns>the eased percentage</returns>
    public static float EaseOut(float percentage) => (float)Math.Pow(percentage, 1.0f / 4);

    /// <summary>
    /// An easing function that starts fast and decellerates as time moves on
    /// </summary>
    /// <param name="percentage">the linear percentage</param>
    /// <returns>the eased percentage</returns>

    public static float EaseOutSoft(float percentage) => (float)Math.Pow(percentage, 1.0f / 2);

    /// <summary>
    /// An easing function that starts and ends slow, but peaks at the midpoint
    /// </summary>
    /// <param name="percentage">the linear percentage</param>
    /// <returns>the eased percentage</returns>
    public static float EaseInOut(float percentage) => percentage < .5 ? 4 * percentage * percentage * percentage : (percentage - 1) * (2 * percentage - 2) * (2 * percentage - 2) + 1;

    public static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }

    public static float EaseInOutCinematic(float t)
    {
        const float edgeStrength = .6f;
        // Clamp for safety
        t = Math.Clamp(t, 0f, 1f);

        // edgeStrength:
        // 0   = almost linear
        // 0.5 = light cinematic
        // 1+  = stronger ease but still no stall

        var a = edgeStrength;

        // Core idea: blend linear with smoothstep-like curve
        var smooth = t * t * (3f - 2f * t); // classic smoothstep
        return (1f - a) * t + a * smooth;
    }

    /// <summary>
    /// An easing function that overshoots its target then bounces back
    /// </summary>
    /// <param name="percentage">the linear percentage</param>
    /// <returns>the eased percentage</returns>
    public static float EaseOverShootAndBounceBack(float percentage)
    {
        var c1 = 1.70158f;
        var c2 = c1 * 1.525f;

        var ret = percentage < 0.5
          ? (Math.Pow(2 * percentage, 2) * ((c2 + 1) * 2 * percentage - c2)) / 2
          : (Math.Pow(2 * percentage - 2, 2) * ((c2 + 1) * (percentage * 2 - 2) + c2) + 2) / 2;
        return (float)ret;
    }
}

public readonly struct CinematicEase
{
    public float StartStrength { get; }
    public float EndStrength { get; }

    public float TotalMilliseconds { get; }

    public CinematicEase(float totalDurationMilliseconds, float startStrength, float endStrength)
    {
        TotalMilliseconds = totalDurationMilliseconds;
        StartStrength = Math.Clamp(startStrength, -1f, 1f);
        EndStrength = Math.Clamp(endStrength, -1f, 1f);
    }

    public float Apply(float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        // Linear cubic Bézier has y control points at 1/3 and 2/3.
        // Move them up/down to shape the beginning and end independently.
        var p1 = (1f / 3f) - (StartStrength / 3f);
        var p2 = (2f / 3f) + (EndStrength / 3f);

        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;

        return (3f * uu * t * p1) + (3f * u * tt * p2) + (tt * t);
    }
}