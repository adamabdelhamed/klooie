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
public static class Program
{
    public static void Main() => new HelloWorld().Run();
}
//#EndSample