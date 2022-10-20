//#Sample -Id ProtectedConsolePanelSample
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
//#EndSample

public class MyApp : ConsoleApp
{
    protected override Task Startup()
    {
        LayoutRoot.Add(new CustomPanel()).Fill();
        return Task.CompletedTask;
    }
}

public class ProtectedConsolePanelRunner : IRecordableSample
{
    public string OutputPath => @"ProtectedConsolePanel\ProtectedConsolePanelSample.gif";
    public int Width => 50;
    public int Height => 3;
    public ConsoleApp Define()
    {
        var app = new MyApp();
        app.Invoke(async () =>
        {
            await Task.Delay(50);
            app.Stop();
        });
        return app;
    }
}