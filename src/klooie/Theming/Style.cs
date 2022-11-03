namespace klooie.Theming;

/// <summary>
/// A rule that targets a property on a ConsoleControl's property
/// </summary>
public sealed class Style
{
    /// <summary>
    /// used internally for usage tracking
    /// </summary>
    internal int Index { get; set; }
    /// <summary>
    /// the type of control that the style applies to
    /// </summary>
    public Type Type { get; private set; }

    /// <summary>
    /// When specified, the style will only be applied when the targeted
    /// control is inside a control of this type
    /// </summary>
    public Type? Within { get; private set; }

    /// <summary>
    /// When specified, the style will only be applied when the targeted
    /// control has this tag.
    /// </summary>
    public string? Tag { get; private set; }

    /// <summary>
    /// When specified, the style will only be applied when the targeted
    /// control is inside of a parent that contains this tag
    /// </summary>
    public string? WithinTag { get; private set; }

    /// <summary>
    /// Specifies which property this style targets
    /// </summary>
    public string PropertyName { get; private set; }

    /// <summary>
    /// The value that the property should be set to when this style applies
    /// </summary>
    public object Value { get; private set; }

    /// <summary>
    /// Creates a new style
    /// </summary>
    /// <param name="t">the type of object being targeted</param>
    /// <param name="propertyName">the property being targeted</param>
    /// <param name="value">the value to set when the style applies</param>
    /// <param name="tag">only apply to targets with this tag</param>
    /// <param name="within">only applies to targets within this type of container</param>
    /// <param name="withinTag">only applies to targets that have a parent with this tag</param>
    public Style(Type t, string propertyName, object value, string? tag = null, Type? within = null, string? withinTag = null)
    {
        this.Type = t;
        this.Tag = tag;
        this.PropertyName = propertyName;
        this.Value = value;
        this.Within = within;
        this.WithinTag = withinTag;
    }

    /// <summary>
    /// gets a string representation of the style
    /// </summary>
    /// <returns>a string representation of the style</returns>
    public override string ToString()
    {
        var ret = $"{Type.Name}.{PropertyName} = {Value}";
        if (Within != null) ret += $" (within {Within.Name})";
        if (Tag != null) ret += $" (tag {Tag})";
        if (WithinTag != null) ret += $" (within tag {WithinTag})";
        return ret;
    }

    internal void ApplyPropertyValue(ConsoleControl c, ILifetimeManager lt)
    {
        var prop = c.GetType().GetProperty(PropertyName);
        if (prop == null) throw new ArgumentException($"Failed to apply style to missing property {PropertyName} on type {c.GetType().FullName}");
        var unstyledValue = prop.GetValue(c);
        prop.SetValue(c, Value);
        lt.OnDisposed(() =>
        {
            if (c.ShouldContinue == false) return;
            prop?.SetValue(c, unstyledValue);
        });
        c.Subscribe(PropertyName, () =>
        {
            if (lt.ShouldContinue == false) return;
            prop.SetValue(c, Value);
        }, lt);
    }
}