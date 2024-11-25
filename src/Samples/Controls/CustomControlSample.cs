//#Sample -Id CustomControlSample
using klooie.Theming;
using PowerArgs;
namespace klooie.Samples;

public partial class CustomControl : ConsoleControl
{
    // look closely at the getter and setter. This syntax makes the properties observable and themeable.
    public partial RGB BorderColor { get; set; }

    public CustomControl()
    {
        Width = 17;
        Height = 5;
        // these are default colors, but can be overridden by the caller or by a theme
        BorderColor = RGB.Orange; 
        Background = RGB.Orange.Darker;
        CanFocus = true;
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        // We have raw access to the image we'll be painting.
        // the ConsoleBitmap class offers drawing utilities.

        // Draw a border as a different color if the control has focus
        context.DrawRect(HasFocus ? FocusColor : BorderColor, 0, 0, Width, Height);
    }
}

// 
// To fully implement a themeable control you should follow this pattern.
// 
// Step 1. Create a styler that exposes a Fluent API for each of your custom properties.
// 
public class CustomControlStyler : StyleBuilder<CustomControlStyler>
{
    public CustomControlStyler(StyleBuilder toWrap) : base(toWrap) { }
    public CustomControlStyler BorderColor(RGB color) => Property(nameof(CustomControl.BorderColor), color);

}

// 
// Step 2. Create an extension method. The ForX naming convention makes it clear that this is a custom styler.
// 
public static class CustomControlThemeExtensions
{
    public static CustomControlStyler ForX<T>(this StyleBuilder builder) where T : CustomControl => new CustomControlStyler(builder).For<T>();
}

//
// Step 3. Use your extension method when defining your themes.
//
public class AppTheme : Theme
{
    // because we implemented the ForX extension method we can now
    // define styles for our custom control's custom property alongside
    // styles for other controls.
    public override Style[] Styles => StyleBuilder.Create()
        .For<Label>().FG(RGB.Green)
        .ForX<CustomControl>().BorderColor(RGB.Magenta).BG(RGB.DarkMagenta)
        .ToArray();
}

public class CustomControlSample : ConsoleApp
{
    protected override async Task Startup()
    {
        new AppTheme().Apply();
        var control = LayoutRoot.Add(new CustomControl()).CenterBoth();
        await Task.Delay(1000);
        control.Focus();
        await Task.Delay(1000);
        control.Unfocus();
        Stop();
    }
}

// Entry point for your application
public static class CustomControlSampleProgram
{
    public static void Main() => new XYChartSample().Run();
}
//#EndSample

public class CustomControlSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\CustomControlSample.gif";
    public int Width => 60;
    public int Height => 25;
    public ConsoleApp Define() => new CustomControlSample();
}