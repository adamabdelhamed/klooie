# Dialogs

klooie has built in support for dialogs. Dialogs are useful when showing confirmations, or quickly gathering input from the user.

## Message Dialog

```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class ShowMessageSample : ConsoleApp
{
    protected override async Task Startup()
    {
        LayoutRoot.Background = RGB.Gray;
        await Task.Delay(1000);
        var wasYes = await MessageDialog.ShowYesOrNo("Are you having fun?".ToWhite());
        await Task.Delay(1000);
        var reply = wasYes ? "Great!".ToGreen() : "Too bad".ToRed();
        await MessageDialog.Show(new ShowMessageOptions(reply) { BorderColor = wasYes ? RGB.Green : RGB.Red });
    }
}

// Entry point for your application
public static class ShowMessageSampleProgram
{
    public static void Main() => new ShowMessageSample().Run();
}

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/ShowMessage/ShowMessageSample.gif?raw=true)
