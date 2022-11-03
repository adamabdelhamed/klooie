//#Sample -Id ButtonSample
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
//#EndSample

public class ButtonSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\ButtonSample.gif";
    public int Width => 120;
    public int Height => 25;
    public ConsoleApp Define() => new ButtonSample();

    public static async Task SimulateUserInput()
    {
        await Task.Delay(2000);
        await ConsoleApp.Current.SendKey(ConsoleKey.Tab);
        await Task.Delay(2000);

        // presses button 1
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await Task.Delay(5000);
        // dismisses dialog
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await Task.Delay(2000);

        //presses button 2
        await ConsoleApp.Current.SendKey(ConsoleKey.B, shift: true);
        await Task.Delay(5000);
        // dismisses dialog
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await Task.Delay(2000);
    }
}