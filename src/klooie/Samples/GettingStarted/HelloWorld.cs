//#Sample -Id HelloWorld
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class HelloWorld : ConsoleApp
{
    protected override Task Startup() =>
        LayoutRoot.Add(new Label("Hello World! Press escape to exit.".ToOrange())).CenterBoth().FadeIn(2000);
}

// Entry point for your application
public static class HelloWorldProgram
{
    public static void Main() => new HelloWorld().Run();
}
//#EndSample


public class HelloWorldRunner : IRecordableSample
{
    public string OutputPath => @"GettingStarted\HelloWorld.gif";

    public int Width => 60;

    public int Height => 5;

    public ConsoleApp Define()
    {
        var ret = new HelloWorld();
        ret.Invoke(async () =>
        {
            await Task.Delay(3000);
            ret.Stop();
        });
        return ret;
    }
}