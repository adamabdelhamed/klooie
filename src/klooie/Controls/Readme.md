The types in this folder represent the built-in controls that you can use in your applications.

## Label

Labels are the most basic controls. They display text. By default, labels cannot be focused. If the Text property of the label is unstyled (default foreground and background color) then the control's Foreground and Background properties will be applied to the text. If the Text property is a styled ConsoleString then the text's style will be used.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/LabelSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class LabelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var stack = LayoutRoot.Add(new StackPanel() { Orientation = Orientation.Vertical }).Fill();
        stack.Background = new RGB(50, 50, 50);
        stack.Add(new Label("Unstyled Text that uses control FG and BG") { Foreground = RGB.White, Background = RGB.DarkYellow });
        stack.Add(new Label("Red Text that uses the default background".ToRed()));
        stack.Add(new Label("Magenta Text that blends over the parent's background".ToMagenta()) { CompositionMode = CompositionMode.BlendBackground });
        stack.Add(new Label(ConsoleString.Parse("[Red]Multi [Green]Colored [Orange]Text [B=Cyan][Black] with custom [D] and blended BG")) { CompositionMode = CompositionMode.BlendBackground });
        await Task.Delay(100);
        Stop();
    }
}

// Entry point for your application
public static class LabelSampleProgram
{
    public static void Main() => new LabelSample().Run();
}

```

## Button

A button can be 'pressed' with the enter key when it has focus. It also supports a shortcut key that can be pressed even when the button does not have focus.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/ButtonSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class ButtonSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var button1 = LayoutRoot.Add(new Button() { Text = "Button with no shortcut".ToOrange() })
            .DockToLeft(padding: 2)
            .CenterVertically();

        var button2 = LayoutRoot.Add(new Button(new KeyboardShortcut(ConsoleKey.B, ConsoleModifiers.Shift)) { Text = "Button with shortcut".ToOrange() })
            .DockToRight(padding: 2)
            .CenterVertically();

        button1.Pressed.Subscribe(async () => await MessageDialog.Show($"{button1.Text} pressed"), button1);
        button2.Pressed.Subscribe(async () => await MessageDialog.Show($"{button2.Text} pressed"), button2);
        
        await ButtonSampleRunner.SimulateUserInput();
        Stop();
    }
}

// Entry point for your application
public static class ButtonSampleProgram
{
    public static void Main() => new ButtonSample().Run();
}

```

## ListViewer

The ListViewer control displays tabular data in a familiar table style with a pager control. It can receive focus via tab and the user can navigate rows using the up and down arrow keys. Here's how to use the ListViewer.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/ListViewerSample.gif?raw=true)
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

## Dropdown

A dropdown lets the user pick from a set of choices.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/DropdownSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class DropdownSample : ConsoleApp
{
    private enum MyEnum
    {
        Value1,
        Value2,
        Value3
    }

    protected override async Task Startup()
    {
        var dropdown = LayoutRoot.Add(new EnumDropdown<MyEnum>()).DockToTop(padding:1).CenterHorizontally();

        await SendKey(ConsoleKey.Tab);
        await Task.Delay(1000);
        await SendKey(ConsoleKey.Enter);
        await Task.Delay(1000);
        await SendKey(ConsoleKey.DownArrow);
        await Task.Delay(1000);
        await SendKey(ConsoleKey.Enter);
        await Task.Delay(1000);
        await MessageDialog.Show(new ShowMessageOptions(ConsoleString.Parse($"Value: [B=Cyan][Black] {dropdown.Value} ")) { MaxLifetime = Task.Delay(3000).ToLifetime(), AllowEnterToClose = false, AllowEscapeToClose = false });
        Stop();
    }
}

// Entry point for your application
public static class DropdownSampleProgram
{
    public static void Main() => new DropdownSample().Run();
}

```

## MinimumSizeShield

Sometimes you only want the user to see a control if it has enough space to display properly. A MinimumSizeShield is helpful in these cases.

Simply fill a container with a MinimumSizeShield and it will display itself along with a helpful message whenever the container is too small to show the other controls.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/MinimumSizeShieldSample.gif?raw=true)
```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class MinimumSizeShieldSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // create some contrast so we can see the effect
        LayoutRoot.Background = RGB.Orange.Darker;

        // in a real app the LayoutRoot itself is resizable by the user,
        // but for this sample we'll simulate the resize using a child panel
        var resizablePanel = LayoutRoot.Add(new ConsolePanel());
        resizablePanel.Width = 1;
        resizablePanel.Height = 1;

        // this app has a form that won't look good unless it has enough space
        resizablePanel.Add(new Form(FormGenerator.FromObject(new SampleFormModel())) { Width = 50, Height = 3 }).CenterBoth();

        // this shield ensures that we don't see the form unless it has enough room
        resizablePanel.Add(new MinimumSizeShield(new MinimumSizeShieldOptions()
        {
            MinHeight = 10,
            MinWidth = 50
        })).Fill();

        // now let's resize the panel and see how the behavior changes as it grows
        for(var w = 1; w < LayoutRoot.Width;w++)
        {
            resizablePanel.Width = w;
            await Task.Delay(30);
        }

        for (var h = 1; h < LayoutRoot.Height; h++)
        {
            resizablePanel.Height = h;
            await Task.Delay(200);
        }
        Stop();
    }
}

// Entry point for your application
public static class MinimumSizeShieldSampleProgram
{
    public static void Main() => new MinimumSizeShieldSample().Run();
}

```


## XY Chart

You can build complex controls with klooie. Here's how to use the built-in XYChart. This is useful when building quick command line apps that visualize data.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/XYChartSample.gif?raw=true)
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

## Custom controls

You can derive from ConsoleControl to create your own controls.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Controls/CustomControlSample.gif?raw=true)
```cs
using PowerArgs;
using klooie;
using klooie.Theming;
namespace klooie.Samples;

public class CustomControl : ConsoleControl
{
    // look closely at the getter and setter. This syntax makes the properties observable and themeable.
    public RGB BorderColor { get => Get<RGB>(); set => Set(value); }

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

```
