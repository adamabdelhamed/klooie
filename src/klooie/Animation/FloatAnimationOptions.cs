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

    public virtual void Set(float percentage) => Setter(percentage);
}

public class FloatAnimationOptions<T> : FloatAnimationOptions
{
    /// <summary>
    /// The action that applies the current animation value when it is time
    /// </summary>
    public Action<T, float> Setter { get; set; }
    /// <summary>
    /// The object that the setter will be called on
    /// </summary>
    public T Target { get; set; }

    public override void Set(float percentage) => Setter(Target, percentage);
}