using klooie;
using PowerArgs.Cli;

var app = new ConsoleApp();
app.Invoke(()=> app.LayoutRoot.Add(new ConsoleBitmapAnimationStudio()).Fill());
app.Run();