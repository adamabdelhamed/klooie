# klooie
A framework for building GUI applications within any command line that runs .NET. Klooie provides all the things you would expect from a UX Framework.

## Key Features

- [**Containers and Layout**](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/klooie/Containers/Readme.md)  Easily organize controls into a usable view Easily organize controls into a usable view
- **Built-in & Custom Controls**  Use controls from the library or create your own
- [**Dialogs**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/klooie/Containers/Dialogs/Readme.md)  Panels that appear over a view and temporarity restrict focus to the controls within the dialog
- **Theming**  A clean model for defining one or more themes for your application.
- **Focus**  Lets the user interact with one primary control at a time
- **Forms** A structured way to accept multiple inputs from the user 
- **Animation** You can animate controls sizes, positions, colors, and more. Built-in easing and custom easing supported.
- **Ansi RGB** klooie writes to the console using ANSI, meaning that a full RGB color set is available

## Getting Started

Here's a hello world console app that just shows a message on the middle of the screen and waits for the user to press escape before exiting.

```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class HelloWorld : ConsoleApp
{
    protected override Task Startup() => LayoutRoot
        .Add(new Label("Hello World! Press escape to exit.".ToOrange()))
        .CenterBoth()
        .FadeIn(2000);
}

// Entry point for your application
public static class HelloWorldProgram
{
    public static void Main() => new HelloWorld().Run();
}

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/GettingStarted/HelloWorld.gif?raw=true)

## Advanced Features


- Filters - Modify the visual image that a control produced before painting it to the screen

### klooie.Gaming

You can have fun with klooie and build games that are fun to play within the command line.

- Physics - Enables controls to move with velocity semantics and collision detection

- Movement - Pathfinding and other components that let controls navigate obstacles in the environment
