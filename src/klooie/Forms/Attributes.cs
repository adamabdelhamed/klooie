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

public sealed class FormDropdownFromEnumAttribute : Attribute
{
    public static readonly DialogChoice NullOption = new DialogChoice()
    {
        DisplayText = "None".ToConsoleString(),
        Id = "None",
        Value = null,
    };

    public Type EnumType { get; private init; }
    public bool Nullable { get; private init; }
    public FormDropdownFromEnumAttribute(Type enumType, bool nullable)
    {
        this.EnumType = enumType;
        this.Nullable = nullable;
    }

    public List<DialogChoice> GetOptions()
    {
        var ret = new List<DialogChoice>();

        if (Nullable)
        {
            ret.Add(NullOption);
        }

        foreach (var value in Enum.GetValues(EnumType))
        {
            var labelAttribute = EnumType.GetField(value.ToString()).Attr<FormLabelAttribute>();
            var label = labelAttribute?.Label ?? value.ToString();
            var choice = new DialogChoice()
            {
                DisplayText = label.ToConsoleString(),
                Id = value.ToString(),
                Value = value,
            };
            ret.Add(choice);
        }
        return ret;
    }
}

public sealed class FormDropdownProviderAttribute : Attribute
{
    public Type DropdownProviderType { get; private init; }
    public FormDropdownProviderAttribute(Type dropdownProviderType) => this.DropdownProviderType = dropdownProviderType;

    public List<DialogChoice> GetOptions()
    {
        var provider = Activator.CreateInstance(DropdownProviderType) as IFormDropdownProvider;
        return provider.Options;
    }
}

public interface IFormDropdownProvider
{
    List<DialogChoice> Options { get; }
}