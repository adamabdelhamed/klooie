namespace klooie;

public class ThemeIgnoreAttribute : Attribute 
{
    public Type ToIgnore { get; private set; }

    public ThemeIgnoreAttribute(Type toIgnore)
    {
        ToIgnore = toIgnore;
    }
}