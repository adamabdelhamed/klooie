namespace klooie;
internal static class DefaultColors
{
    public static RGB BackgroundColor { get; set; }
    public static RGB ForegroundColor { get; set; }
    public static RGB FocusColor { get; set; }
    public static RGB FocusContrastColor { get; set; }
    public static RGB DisabledColor { get; set; }

    static DefaultColors()
    {
        BackgroundColor = ConsoleString.DefaultBackgroundColor;
        ForegroundColor = ConsoleString.DefaultForegroundColor;
        FocusColor = RGB.Cyan;
        FocusContrastColor = RGB.Black;
        DisabledColor = RGB.DarkGray;
    }
}
