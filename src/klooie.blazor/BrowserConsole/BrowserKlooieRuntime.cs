using klooie.blazor.Demo;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKlooieRuntime : IDisposable
{
    private readonly DemoApp app;
    private readonly BrowserKlooieTerminalHost host;
    private bool disposed;

    public BrowserKlooieRuntime()
    {
        BrowserKlooieTerminalHost.InitConsoleProvider();
        Bitmap = new BrowserConsoleBitmap(BrowserKlooieTerminalHost.TerminalWidth, BrowserKlooieTerminalHost.TerminalHeight);
        app = new DemoApp();
        app.StartCooperative();
        host = new BrowserKlooieTerminalHost(Bitmap);
        ((LayoutRootPanel)app.LayoutRoot).TerminalHost = host;
        host.SyncSize((LayoutRootPanel)app.LayoutRoot);
    }

    public BrowserConsoleBitmap Bitmap { get; }

    public void Tick(TimeSpan budget)
    {
        if (disposed) return;
        app.Tick(budget);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        app.StopCooperative();
        app.TryDispose(app.Lease, "BrowserKlooieRuntime disposed");
    }
}
