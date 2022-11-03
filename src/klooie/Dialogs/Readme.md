# Dialogs

klooie has built in support for dialogs. Dialogs are useful when showing confirmations, or quickly gathering input from the user.

## Message Dialog

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/ShowMessage/ShowMessageSample.gif?raw=true)
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

## Text Input Dialog

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/ShowTextInput/ShowTextInputSample.gif?raw=true)
```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class ShowTextInputSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var name = await TextInputDialog.Show("What is your name?".ToYellow());
        if (name != null)
        {
            await Task.Delay(250);
            LayoutRoot.Add(new Label($"Hello {name}!")).CenterBoth();
        }
        await Task.Delay(1000);
        Stop();
    }
}

// Entry point for your application
public static class ShowTextInputSampleProgram
{
    public static void Main() => new ShowTextInputSample().Run();
}

```
