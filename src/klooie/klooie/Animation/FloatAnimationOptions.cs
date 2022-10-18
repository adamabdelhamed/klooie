namespace klooie;

/// <summary>
/// Options for doing animations
/// </summary>
public class FloatAnimationOptions : CommonAnimationOptions
{
    /// <summary>
    /// The starting value of the animated property
    /// </summary>
    public float From { get; set; }

    /// <summary>
    /// The final value of the animated property
    /// </summary>
    public float To { get; set; }

    /// <summary>
    /// The action that applies the current animation value when it is time
    /// </summary>
    public Action<float> Setter { get; set; }
}