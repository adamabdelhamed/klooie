//#Sample -Id XYChartSample
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
//#EndSample

public class XYChartSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\XYChartSample.gif";
    public int Width => 60;
    public int Height => 25;
    public ConsoleApp Define() => new XYChartSample();
}