namespace klooie;
public sealed class RGBAnimationOptions : CommonAnimationOptions
{
    public List<KeyValuePair<RGB, RGB>> Transitions { get; set; } = new List<KeyValuePair<RGB, RGB>>();
    public Action<RGB[]> OnColorsChanged { get; set; }
}