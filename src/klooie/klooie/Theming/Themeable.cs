namespace klooie;

public sealed class ThemeIgnoreAttribute : Attribute 
{
    public Type ToIgnore { get; private set; }

    public ThemeIgnoreAttribute(Type toIgnore)
    {
        ToIgnore = toIgnore;
    }
}