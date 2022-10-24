# Containers and Layout

klooie provides several containers that make it easy to layout the controls within your application.

## ConsolePanel

A ConsolePanel is the most basic type of container. You are responsible for sizing and positioning the controls that you add to a panel.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/ConsolePanelSample.gif?raw=true)
```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class ConsolePanelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // every app comes with a root ConsolePanel called LayoutRoot
        ConsolePanel consolePanel = LayoutRoot;

        consolePanel.Add(new Label("Docked right, centered vertically".ToGreen()))
            .DockToRight()
            .CenterVertically();

        consolePanel.Add(new Label("Docked left, centered vertically".ToGreen()))
            .DockToLeft()
            .CenterVertically();

        consolePanel.Add(new Label("Docked top, centered horizontally".ToGreen()))
          .DockToTop()
          .CenterHorizontally();

        consolePanel.Add(new Label("Docked bottom, centered horizontally".ToGreen()))
           .DockToBottom()
           .CenterHorizontally();

        consolePanel.Add(new Label("center both".ToOrange()))
            .CenterBoth();

        consolePanel.Add(new Label("Docked bottom left with padding".ToMagenta()))
           .DockToBottom(padding:2)
           .DockToLeft(padding: 4);

        consolePanel.Add(new Label("Docked bottom right with padding".ToMagenta()))
         .DockToBottom(padding: 2)
         .DockToRight(padding: 4);

        consolePanel.Add(new Label("Docked top left with padding".ToMagenta()))
           .DockToTop(padding: 2)
           .DockToLeft(padding: 4);

        consolePanel.Add(new Label("Docked top right with padding".ToMagenta()))
         .DockToTop(padding: 2)
         .DockToRight(padding: 4);

        await LayoutRoot.FadeIn(1000);
    }
}

// Entry point for your application
public static class ConsolePanelSampleProgram
{
    public static void Main() => new ConsolePanelSample().Run();
}

```

## GridLayout

A GridLayout makes it very easy to create more advanced layouts. Here's a basic example that shows how to configure a grid.

This sample shows a common layout where there is a fixed size menu on the left and a main panel that takes up the remaining space and shows some controls.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/GridLayoutSample.gif?raw=true)
```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class GridLayoutSample : ConsoleApp
{
    private static readonly List<string> menuItems = new List<string>() { "Menu item 1", "Menu item 2", "Menu item 3", "Menu item 4", };
    private ConsolePanel leftPanel, mainPanel;
    private Menu<string> menu;
    protected override async Task Startup()
    {
        CreateGridLayout();
        InitializeMenu();
        await SimulateUserBehavior();
        Stop();
    }

    private void CreateGridLayout()
    {
        // only one row that takes 100% of the height
        var rowSpec = "100%";
        // 2 columns...the first is 15 pixels and the second takes up the remaining space
        var colSpec = "15p;1r";

        // add the grid and call Fill() to ensure that it fills its parent's entire space
        var gridLayout = LayoutRoot.Add(new GridLayout(rowSpec, colSpec)).Fill();

        // Add the left panel to the first column and the main panel to the second column
        leftPanel = gridLayout.Add(new ConsolePanel() { Background = RGB.Orange.Darker }, 0, 0);
        mainPanel = gridLayout.Add(new ConsolePanel() { Background = RGB.Orange }, 1, 0);
    }

    private void InitializeMenu()
    {
        // all items are enabled
        var isEnabled = (string item) => true;
        // items are displayed as black
        var formatter = (string item) => item.ToBlack();
        // add the menu to the left panel with some padding
        menu = leftPanel.Add(new Menu<string>(menuItems, isEnabled, formatter)).Fill(padding: new Thickness(2, 0, 1, 0));

        // makes sure the labels blend nicely with the orange background
        menu.CompositionMode = CompositionMode.BlendBackground;

        // whenever a menu item is activated we will call ShowSelectedItem()
        menu.ItemActivated.Subscribe((selectedItem)=>
        {
            mainPanel.Controls.Clear();
            mainPanel.Add(new Label(selectedItem.ToBlack()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
        }, menu);

        // give the menu focus so that the user can use arrow keys or tab to navigate the menu
        menu.Focus();
    }

    private async Task SimulateUserBehavior()
    {
        menu.SelectedIndex = 0;
        menu.ItemActivated.Fire(menu.SelectedItem);
        for (var i = 1; i < menuItems.Count; i++)
        {
            await Task.Delay(1000);
            menu.SelectedIndex = i;
            menu.ItemActivated.Fire(menu.SelectedItem);
        }
    }
}

// Entry point for your application
public static class GridLayoutSampleProgram
{
    public static void Main() => new GridLayoutSample().Run();
}

```

## StackPanel

A StackPanel stacks a set of controls vertically or horizontally.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/StackPanelSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class StackPanelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var vStack = LayoutRoot.Add(new StackPanel() { Margin = 1, Background = RGB.Black, Orientation = Orientation.Vertical, Width = LayoutRoot.Width/2}).DockToLeft().FillVertically();
        var hStack = LayoutRoot.Add(new StackPanel() { Margin = 2, Background = RGB.White, Orientation = Orientation.Horizontal, Width = LayoutRoot.Width / 2 }).DockToRight().FillVertically();

        for(var i = 1; i <=vStack.Height/2; i++)
        {
            await vStack.Add(new Label($"Vertical label {i}")).FadeIn();
        }

        var random = new Random();
        var standardColors = RGB.ColorsToNames.Keys.ToArray();
        for (var i = 1; i <= vStack.Width/3; i++)
        {
            await hStack.Add(new Label($"H".ToConsoleString(standardColors[random.Next(0, standardColors.Length)])) { CompositionMode = CompositionMode.BlendBackground }).FadeIn();
        }

        await Task.Delay(1000);
        Stop();
    }
}

// Entry point for your application
public static class StackPanelSampleProgram
{
    public static void Main() => new StackPanelSample().Run();
}

```

## TabControl

A TabControl lets you build a view with multiple horizontal tabs that switch the main content below them.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/TabControlSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class TabControlSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var tabs = LayoutRoot.Add(new TabControl(new TabControlOptions("Tab1", "Tab2", "Tab3")
        {
            TabAlignment = TabAlignment.Left,
            BodyFactory = (string selectedTab) =>
            {
                var panel = new ConsolePanel() { Background = new RGB(20, 20, 20) };
                panel.Add(new Label($"You selected tab {selectedTab}".ToOrange()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
                return panel;
            }
        })
        { Background = new RGB(10,10,10) }).Fill();

        
        for(var i = 0; i < tabs.Options.Tabs.Count; i++)
        {
            await SendKey(ConsoleKey.Tab);// simulate the user bringing the next tab in focus
            await Task.Delay(1000);
        }

        for (var i = 0; i < tabs.Options.Tabs.Count; i++)
        {
            await SendKey(ConsoleKey.Tab.KeyInfo(shift:true));// the user can shift+tab to go backwards
            await Task.Delay(1000);
        }

        await Task.Delay(1000);
        Stop();
    }
}

// Entry point for your application
public static class TabControlSampleProgram
{
    public static void Main() => new TabControlSample().Run();
}

```

## ScrollablePanel

A ScrollablePanel lets you fit large panels inside a smaller one and provides scrollbars and keyboard shortcuts to navigate the larger content.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/ScrollableSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class ScrollableSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // this panel will scroll anything added to it's ScrollableContent property
        var scrollable = LayoutRoot.Add(new ScrollablePanel()).Fill(padding: new Thickness(2,2,1,1));
        var stack = scrollable.ScrollableContent.Add(new StackPanel() { Orientation = Orientation.Vertical, AutoSize = StackPanel.AutoSizeMode.Both });
        
        // We make sure that the scrollable content size is the same as the stack of controls. If we forget to do this then the
        // stack will outgrow the scrollable content and it won't scroll properly.
        stack.Sync(nameof(stack.Bounds), () => scrollable.ScrollableContent.ResizeTo(stack.Width, stack.Height), stack);

        for(var i = 0; i < 100; i++)
        {
            stack.Add(new Label($"Row {i}"));
        }

        // simulate the user moving focus to the scrollbar
        await SendKey(ConsoleKey.Tab);
        
        // simulate user manually scrolling down
        for (var i = 0; i < 100-scrollable.Height; i++)
        {
            await SendKey(ConsoleKey.DownArrow);
            await Task.Delay(50);
            var sb = scrollable.Descendents.WhereAs<Scrollbar>().ToArray();
        }

        await Task.Delay(1000);

        // simulate the user pressing Page Up to scroll up faster
        for (var i = 0; i < 10; i++)
        {
            await SendKey(ConsoleKey.PageUp);
            await Task.Delay(200);
            var sb = scrollable.Descendents.WhereAs<Scrollbar>().ToArray();
        }

        await Task.Delay(1000);

        // simulate the user pressing End to get back to the bottom
        await SendKey(ConsoleKey.End);
        await Task.Delay(1000);

        // simulate the user pressing Home to get back to the top
        await SendKey(ConsoleKey.Home);
        await Task.Delay(1000);

        Stop();
    }
}

// Entry point for your application
public static class ScrollableSampleProgram
{
    public static void Main() => new ScrollableSample().Run();
}

```

## DataGallery

A DataGallery displays a set of controls as a set of tiles in a flow layout.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/DataGallerySample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class DataGallerySample : ConsoleApp
{
    protected override async Task Startup()
    {
        var gallery = LayoutRoot.Add(new DataGallery<string>((theString, index) =>
        {
            var tile = new ConsolePanel() { Width = 18, Height = 8, Background = new RGB(20, 20, 20) };
            tile.Add(new Label(theString.ToOrange()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
            return tile;
        })
        { Background = new RGB(50,50,50) }).Fill();

        await Task.Delay(1500);
        var tiles = new string[]
        {
            "These",
            "tiles",
            "wrap",
            "nicely",
            "in",
            "a",
            "flow",
            "layout"
        };
        for (var i = 1; i <= tiles.Length; i++)
        {
            gallery.Show(tiles.Take(i));
            await Task.Delay(200);
        }
        await Task.Delay(1500);
        Stop();
    }
}

// Entry point for your application
public static class DataGallerySampleProgram
{
    public static void Main() => new DataGallerySample().Run();
}

```

## FixedAspectRatioPanel

A FixedAspectRatioPanel lets hosts another control and maintains its desired aspect ratio as it is resized. 

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Containers/FixedAspectRatioPanelSample.gif?raw=true)
```cs
using PowerArgs;
namespace klooie.Samples;

public class FixedAspectRatioPanelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // this panel is resizable, simulating what happens in a real app where the user can
        // resize the window to whatever shape they prefer
        var resizablePanel = LayoutRoot.Add(new ConsolePanel() { Width = 1, Height = 1 });

        // this magenta colored panel is designed to only support a 2 * 1 aspect ratio
        var magentaPanelWithFixedAspectRatio = new ConsolePanel() { Background = RGB.Magenta };

        // We use a FixedAspectRatioPanel to host the magenta panel. As the resizable panel changes size
        // this fixed aspect ratio panel will keep our magenta panel at the correct aspect ratio.
        var whiteFixedAspectRatioPanel = resizablePanel.Add(new FixedAspectRatioPanel(2f / 1f, magentaPanelWithFixedAspectRatio) { Background = RGB.White }).Fill();

        // simulate resizing the window so we can see the fixed aspect ratio panel do its thing
        await resizablePanel.AnimateAsync(new ConsoleControlAnimationOptions() { Destination = ()=> LayoutRoot.Bounds, Duration = 3000, AutoReverse = true });

        Stop();
    }
}

// Entry point for your application
public static class FixedAspectRatioPanelSampleProgram
{
    public static void Main() => new FixedAspectRatioPanelSample().Run();
}

```

## ProtectedConsolePanel

A ProtectedConsolePanel lets you create a custom container that blocks consumers from accessing the children directly.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/ProtectedConsolePanel/ProtectedConsolePanelSample.gif?raw=true)
```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class CustomPanel : ProtectedConsolePanel
{
    public CustomPanel()
    {
        // Consumers of CustomPanel will not be able to access the ProtectedPanel property.
        // This means that you don't have to worry about them calling something like ProtectedPanel.Controls.Clear();
        this.ProtectedPanel.Add(new Label("Some label".ToGreen())).CenterBoth();
    }
}

```
