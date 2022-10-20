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
