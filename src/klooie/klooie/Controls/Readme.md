The types in this folder represent the built-in controls that you can use in your applications.

## XY Chart

You can build complex controls with klooie. Here's how to use the built-in XYChart. This is useful when building quick command line apps that visualize data.

```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class XYChartSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var parabolaData = Enumerable.Range(-100, 200)
            .Select(x => new DataPoint() { X = x, Y = x * x })
            .ToList();

        LayoutRoot.Add(new XYChart(new XYChartOptions()
        {
            Data = new List<Series>() 
            {
                new Series()
                {
                    Points = parabolaData,
                    PlotCharacter = new ConsoleCharacter('X',RGB.Green),
                    PlotMode = PlotMode.Points,
                    Title = "Parabola",
                    AllowInteractivity = false,
                }
            }
        })).Fill();
        await Task.Delay(5000);
        Stop();
    }
}

// Entry point for your application
public static class XYChartSampleProgram
{
    public static void Main() => new XYChartSample().Run();
}

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Controls/XYChartSample.gif?raw=true)

## Custom controls

You can derive from ConsoleControl to create your own controls.

```cs
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

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Controls/CustomControlSample.gif?raw=true)
