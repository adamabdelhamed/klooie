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

[AttributeUsage(AttributeTargets.Property)]
public sealed class FormSelectAllOnFocusAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class FormContrastAttribute : Attribute { }

/// <summary>
/// An attribute that tells the form generator to use yes no labels for a toggle
/// </summary>
public sealed class FormYesNoAttribute : Attribute { }

public sealed class FormSliderAttribute : Attribute
{
    public RGB BarColor { get; set; } = RGB.White;
    public RGB HandleColor { get; set; } = RGB.Gray;
    public float Min { get; set; } = 0;
    public float Max { get; set; } = 100;
    public float Value { get; set; } = 0;
    public float Increment { get; set; } = 10;
    public bool EnableWAndSKeysForUpDown { get; set; } = false;

    public Slider Factory()
    {
        return new Slider()
        {
            BarColor = BarColor,
            HandleColor = HandleColor,
            Min = Min,
            Max = Max,
            Value = Value,
            Increment = Increment,
            EnableWAndSKeysForUpDown = EnableWAndSKeysForUpDown
        };
    }
}

/// <summary>
/// An attribute that tells the form generator to give this
/// property a specific value width
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FormWidth : Attribute
{
    public int Width { get; private set; }

    /// <summary>
    /// Creates a new FormWidth attribute
    /// </summary>
    /// <param name="width"></param>
    public FormWidth(int width)
    {
        this.Width = width;
    }
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