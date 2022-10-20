The types in this folder represent the main components of a klooie application.

**EventLoop** is a loop that has a custom SynchronizationContext which ensures that async calls are processed correctly on the UI thread.

**ConsoleApp** is the main construct that represents an app. It derives from EventLoop. It defines the control tree, exposes focus, handles window resizing, and provides some additional helpers.

```cs
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

```

**FocusManager** is an internal helper that manages control focus. It is exposed via ConsoleApp.

**ISoundProvider** is an interface that defines Sound APIs. Unfortunately there is no cross platform sound in .NET. See **klooie.Windows** for an implementation of ISoundProvider that is compatible with klooie on Windows.
