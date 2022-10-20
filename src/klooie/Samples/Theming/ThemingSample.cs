//#Sample -Id ThemingSample
using PowerArgs;
using klooie;
using klooie.Theming;

namespace klooie.Samples;

// Define your application
public class ThemingSample : ConsoleApp
{
    private static readonly List<string> menuItems = new List<string>() { "Menu item 1", "Menu item 2", "Menu item 3", "Menu item 4", };
    private ConsolePanel leftPanel;
    protected override async Task Startup()
    {
        CreateGridLayout();
        InitializeMenu();
        using (var themeLifetime = this.CreateChildLifetime())
        {
            new OrangeTheme().Apply(lt: themeLifetime);
            await Task.Delay(3000);
        }

        using (var themeLifetime = this.CreateChildLifetime())
        {
            new DarkTheme().Apply(lt: themeLifetime);
            await Task.Delay(3000);
        }

        using (var themeLifetime = this.CreateChildLifetime())
        {
            new LightTheme().Apply(lt: themeLifetime);
            await Task.Delay(3000);
        }

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
        leftPanel = gridLayout.Add(new LeftPanel(), 0, 0);
        gridLayout.Add(new MainPanel(), 1, 0);
    }

    private void InitializeMenu()
    {
        // add the menu to the left panel with some padding
        leftPanel.Add(new Menu<string>(menuItems)).Fill(padding: new Thickness(2, 0, 1, 0));
    }

    private class LeftPanel : ConsolePanel { }
    private class MainPanel : ConsolePanel { }

    private class OrangeTheme : MyTheme
    {
        protected override RGB LeftPanelColor => RGB.Orange.Darker;
        protected override RGB MainPanelColor => RGB.Orange;
        protected override RGB MenuItemColor => RGB.White;
    }

    private class DarkTheme : MyTheme
    {
        protected override RGB LeftPanelColor => RGB.Black;
        protected override RGB MainPanelColor => new RGB(40,40,40);
        protected override RGB MenuItemColor => RGB.White;
    }

    private class LightTheme : MyTheme
    {
        protected override RGB LeftPanelColor => new RGB(230, 230, 230);
        protected override RGB MainPanelColor => new RGB(250, 250, 250);
        protected override RGB MenuItemColor => RGB.Black;
    }

    private abstract class MyTheme : Theme
    {
        protected abstract RGB LeftPanelColor { get; }
        protected abstract RGB MainPanelColor { get; }
        protected abstract RGB MenuItemColor { get; }

        public override Style[] Styles => StyleBuilder.Create()
            .For<LeftPanel>().BG(LeftPanelColor)
            .For<MainPanel>().BG(MainPanelColor)
            .For<Menu<string>>().BG(LeftPanelColor)
            .For<Label>().FG(MenuItemColor)
            .ToArray();
    }
}

// Entry point for your application
public static class ThemingSampleProgram
{
    public static void Main() => new ThemingSample().Run();
}
//#EndSample


public class ThemingSampleRunner : IRecordableSample
{
    public string OutputPath => @"Theming\ThemingSample.gif";
    public int Width => 60;
    public int Height => 25;

    public ConsoleApp Define() => new ThemingSample();
}