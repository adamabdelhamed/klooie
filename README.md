﻿# klooie
A framework for building GUI applications within any command line that runs .NET. Klooie provides all the things you would expect from a UX Framework.

I'm working on a free video game called [cliborg](https://cliborg.azurewebsites.net?src=klooie) that runs on the command line and highlights many features of this framework.

## Binary
[klooie](https://www.nuget.org/packages/klooie) is available at the Official NuGet Gallery.

## Key Features

Category                                                                                                                    | Description
----------------------------------------------------------------------------------------------------------------------------|------------
[**Containers and Layout**](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Containers/Readme.md)      | Easily organize controls into a usable view Easily organize controls into a usable view
[**Built-in and Custom Controls**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Controls/Readme.md) | Use controls from the library or create your own
[**Dialogs**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Dialogs/Readme.md)            | Panels that appear over a view and temporarity restrict focus to the controls within the dialog
[**Theming**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Theming/Readme.md)                       | A model for defining one or more themes for your application.
[**Focus** **&** **Keyboard** **Input**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Focus/Readme.md)                           | Lets the user interact with one primary control at a time
[**Forms**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Forms/Readme.md)                           | A structured way to accept multiple inputs from the user 
[**Animations**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Animation/Readme.md)                  | You can animate controls sizes, positions, colors, and more. Built-in easing and custom easing supported.
[**Observability**](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Observability/Readme.md)           | Constructs that make your application responsive and dynamic.

## Getting Started

Here's a hello world console app that just shows a message on the middle of the screen and waits for the user to press escape before exiting.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/GettingStarted/HelloWorld.gif?raw=true)
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

## Advanced Features

You can have fun with klooie and build games that are fun to play within the command line.

Category                                                                                                                                          | Description
--------------------------------------------------------------------------------------------------------------------------------------------------|------------
[**Physics**](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Gaming/Readme.md)			                                  | Enables controls to move with velocity semantics and collision detection
[**Sound** **effects** (Windows only)](https://github.com/adamabdelhamed/klooie/tree/main/src/klooie/Audio/Readme.md)                      | Play sound effects and background music
