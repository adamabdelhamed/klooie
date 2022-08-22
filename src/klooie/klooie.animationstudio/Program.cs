using PowerArgs.Cli;

static void Main(string[] args) => new ConsoleApp(() => ConsoleApp.Current.LayoutRoot.Add(new ConsoleBitmapAnimationStudio()).Fill()).Run();