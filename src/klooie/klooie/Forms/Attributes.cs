namespace klooie;
/// <summary>
/// An attribute that tells the form generator to ignore this
/// property
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FormIgnoreAttribute : Attribute { }

/// <summary>
/// An attribute that tells the form generator to give this
/// property a read only treatment
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FormReadOnlyAttribute : Attribute { }

/// <summary>
/// An attribute that tells the form generator to give this
/// property a specific value width
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FormWidth : Attribute
{
    public int Width { get; private init; }

    /// <summary>
    /// Creates a new FormWidth attribute
    /// </summary>
    /// <param name="width">the width of the field control</param>
    public FormWidth(int width) => this.Width = width;
}

/// <summary>
/// An attribute that lets you override the display string 
/// on a form element
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Field)]
public sealed class FormLabelAttribute : Attribute
{
    /// <summary>
    /// The label to display on the form element
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Initialized the attribute
    /// </summary>
    /// <param name="label">The label to display on the form element</param>
    public FormLabelAttribute(string label) { this.Label = label; }
}