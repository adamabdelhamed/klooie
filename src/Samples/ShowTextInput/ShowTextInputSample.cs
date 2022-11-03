//#Sample -Id ShowTextInputSample
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
//#EndSample


public class ShowTextInputSampleRunner : IRecordableSample
{
    public string OutputPath => @"ShowTextInput\ShowTextInputSample.gif";

    public int Width => 60;

    public int Height => 25;

    public ConsoleApp Define()
    {
        var ret = new ShowTextInputSample();

        ret.Invoke(async () =>
        {
            await Task.Delay(2000);
            await ret.SendKey(ConsoleKey.A,shift:true);
            await Task.Delay(400);
            await ret.SendKey(ConsoleKey.D);
            await Task.Delay(400);
            await ret.SendKey(ConsoleKey.A);
            await Task.Delay(400);
            await ret.SendKey(ConsoleKey.M);
            await Task.Delay(1000);
            await ret.SendKey(ConsoleKey.Enter);
        });
        return ret;
    }
}