The types in this folder represent the built-in controls that you can use in your applications.

## ListViewer

The ListViewer control displays tabular data in a familiar table style with a pager control. It can receive focus via tab and the user can navigate rows using the up and down arrow keys. Here's how to use the ListViewer.

```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Description { get; set; }
}

public class ListViewerSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var listViewer = LayoutRoot.Add(new ListViewer<Person>(new ListViewerOptions<Person>()
        {
            DataSource = Enumerable
                .Range(0, 100)
                .Select(i => new Person() { FirstName = "First_" + i, LastName = "Last_" + i, Description = "Description_which_can_be_long" + i })
                .ToList(),
            SelectionMode = ListViewerSelectionMode.Row,
            Columns = new List<HeaderDefinition<Person>>()
            {
                new HeaderDefinition<Person>()
                {
                    Formatter = (o) => new Label(o.FirstName.ToConsoleString().ToRed()),
                    Header = "First Name".ToYellow(),
                    Width = 1,
                    Type = GridValueType.RemainderValue,
                },
                new HeaderDefinition<Person>()
                {
                    Formatter = (o) => new Label(o.LastName.ToConsoleString().ToMagenta()),
                    Header = "Last Name".ToYellow(),
                    Width = 2,
                    Type = GridValueType.RemainderValue,
                }, 
                new HeaderDefinition<Person>()
                {
                    Formatter = (o) => new Label(o.Description.ToOrange()),
                    Header = "Description".ToYellow(),
                    Width = 5,
                    Type = GridValueType.RemainderValue,
                },
            }
        })).Fill();
        
        await Task.Delay(500);

        // simulate the user navigating through the list
        listViewer.Focus();
        for(var i = 0; i < 100; i++)
        {
            await SendKey(ConsoleKey.DownArrow);
            await Task.Delay(50);
        }

        Stop();
    }
}

// Entry point for your application
public static class ListViewerSampleProgram
{
    public static void Main() => new ListViewerSample().Run();
}

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Controls/ListViewerSample.gif?raw=true)

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
