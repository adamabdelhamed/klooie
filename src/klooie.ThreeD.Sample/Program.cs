

using klooie;
using PowerArgs;

VeldridTerminalHost.Init(); // Warms up the infrastructure so that the ConsoleApp infra knows it's not in a "real" console environment and so that the terminal host is ready to go when we attach it to the root panel.
var app = new ConsoleApp();
app.Invoke(() =>
{
    VeldridTerminalHost.BoardZoom = 7; // Zoomed in to show the effect more clearly; adjust as needed (1 is normal)
    VeldridTerminalHost.Attach((LayoutRootPanel)app.LayoutRoot);
    // Large 3d letters centered on the screen
    app.LayoutRoot.Add(new Label("3D Sample".ToYellow())).CenterBoth();
    app.LayoutRoot.Filters.Add(new BorderFilter());
});

app.Run();

public class BorderFilter : IConsoleControlFilter
{
    public ConsoleControl Control { get; set; }
    public ConsoleBitmap ParentBitmap { get; set; }
    private ConsoleCharacter pen = new ConsoleCharacter('/', RGB.Orange);
    public void Filter(ConsoleBitmap bitmap)
    {
        bitmap.DrawRect(pen, 0, 0, bitmap.Width, bitmap.Height);
    }
}