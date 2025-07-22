namespace klooie;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SynthDescriptionAttribute : Attribute
{
    public string Description { get; }
    public SynthDescriptionAttribute(string description) => Description = description;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class SynthCategoryAttribute : Attribute
{
    public string Category { get; }
    public SynthCategoryAttribute(string category) => Category = category;
}
