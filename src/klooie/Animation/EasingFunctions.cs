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
