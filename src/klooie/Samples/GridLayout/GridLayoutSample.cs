//#Sample -Id GridLayoutSample
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
            await Task.Delay(2000);
            menu.SelectedIndex = i;
            menu.ItemActivated.Fire(menu.SelectedItem);
        }

        await Task.Delay(1000);
    }
}

// Entry point for your application
public static class GridLayoutSampleProgram
{
    public static void Main() => new GridLayoutSample().Run();
}
//#EndSample

public class GridLayoutSampleRunner : IRecordableSample
{
    public string OutputPath => @"GridLayout\GridLayoutSample.gif";
    public int Width => 60;
    public int Height => 25;
    public ConsoleApp Define() => new GridLayoutSample();
}