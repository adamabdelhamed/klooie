namespace klooie;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Method)]
public sealed class SynthDocumentationAttribute : Attribute
{
    public string Markdown { get; }
    public SynthDocumentationAttribute(string markdown) => Markdown = markdown;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SynthCategoryAttribute : Attribute
{
    public string Category { get; }
    public SynthCategoryAttribute(string category) => Category = category;
}
