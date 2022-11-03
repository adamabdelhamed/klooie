//#Sample -Id ShowMessageSample
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
//#EndSample


public class ShowMessageSampleRunner : IRecordableSample
{
    public string OutputPath => @"ShowMessage\ShowMessageSample.gif";

    public int Width => 60;

    public int Height => 25;

    public ConsoleApp Define()
    {
        var ret = new ShowMessageSample();

        ret.Invoke(async () =>
        {
            await Task.Delay(2000);
            await ret.SendKey(ConsoleKey.Tab);
            await Task.Delay(1000);
            await ret.SendKey(ConsoleKey.Enter);
            await Task.Delay(3000);
            await ret.SendKey(ConsoleKey.Enter);
            await Task.Delay(500);
            ret.Stop();
        });
        return ret;
    }
}