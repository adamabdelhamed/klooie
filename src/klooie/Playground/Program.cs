using klooie;
using PowerArgs;

var app = new ConsoleApp();

app.Invoke(async () =>
{
    var options = new XYChartOptions()
    {
        Title = "Some fricken chart".ToOrange(),
        Data = new List<Series>()
        {
            new Series()
            {
                Title = "Series1",
                Points = Enumerable.Range(0,100).Select(x => new DataPoint(){ X = x, Y = x*x }).ToList(),
                AllowInteractivity = true,
                PlotCharacter = new ConsoleCharacter('X',RGB.Red),
                PlotMode = PlotMode.Points
            },
             new Series()
            {
                Title = "Series2",
                Points = Enumerable.Range(0,100).Select(x => new DataPoint(){ X = x, Y = 100 * x }).ToList(),
                AllowInteractivity = true,
                PlotCharacter = new ConsoleCharacter('O',RGB.Green),
                PlotMode = PlotMode.Points
            }
        }
    };
    var chart = app.LayoutRoot.Add(new XYChart(options)).Fill();
});

app.Run();