//#Sample -Id TabControlSample
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
//#EndSample

public class TabControlSampleRunner : IRecordableSample
{
    public string OutputPath => @"Containers\TabControlSample.gif";
    public int Width => 60;
    public int Height => 25;
    public ConsoleApp Define() => new TabControlSample();

}