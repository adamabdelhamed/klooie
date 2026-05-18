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
        FrameBuffer = new BrowserConsoleFrameBuffer(80, 25);
        app = new DemoApp();
        app.StartCooperative();
        host = new BrowserKlooieTerminalHost(FrameBuffer);
        ((LayoutRootPanel)app.LayoutRoot).TerminalHost = host;
        host.SyncSize((LayoutRootPanel)app.LayoutRoot);
    }

    public BrowserConsoleFrameBuffer FrameBuffer { get; }

    public BrowserConsoleFrame Tick(int width, int height, TimeSpan budget)
    {
        if (disposed) return BrowserConsoleFrame.Empty;
        host.Resize(width, height);
        app.Tick(budget);
        return FrameBuffer.ToFrame();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        app.StopCooperative();
        app.TryDispose(app.Lease, "BrowserKlooieRuntime disposed");
    }
}
