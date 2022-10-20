//#Sample -Id CustomControlSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class CustomControl : ConsoleControl
{
    // look closely at the getter and setter. This syntax makes the properties observable and themeable.
    public RGB BorderColor { get => Get<RGB>(); set => Set(value); }

    public CustomControl()
    {
        Width = 17;
        Height = 5;
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

public class CustomControlSample : ConsoleApp
{
    protected override async Task Startup()
    {
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