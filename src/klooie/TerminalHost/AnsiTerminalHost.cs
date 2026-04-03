using System.Diagnostics;

namespace klooie;

public sealed class AnsiTerminalHost : ITerminalHost
{
    private int lastConsoleWidth, lastConsoleHeight;

    public IDisposable BeginFrame(LayoutRootPanel root) => NoopDisposable.Instance;

    public bool Present(LayoutRootPanel root, ConsoleBitmap bitmap) => ConsolePainter.Paint(bitmap);

    public bool SyncSize(LayoutRootPanel root)
    {
        if (lastConsoleWidth == 0 && lastConsoleHeight == 0)
        {
            lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
            lastConsoleHeight = ConsoleProvider.Current.WindowHeight;
            root.ResizeTo(lastConsoleWidth, lastConsoleHeight);
            return true;
        }

        if (lastConsoleWidth == ConsoleProvider.Current.BufferWidth && lastConsoleHeight == ConsoleProvider.Current.WindowHeight) return false;

        ConsolePainter.ClearToBlack();

        var lastSyncTime = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(lastSyncTime) <= TimeSpan.FromSeconds(.05f))
        {
            if (ConsoleProvider.Current.BufferWidth != lastConsoleWidth || ConsoleProvider.Current.WindowHeight != lastConsoleHeight)
            {
                lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
                lastConsoleHeight = ConsoleProvider.Current.WindowHeight;
                lastSyncTime = Stopwatch.GetTimestamp();
                ConsolePainter.ClearToBlack();
            }
        }
        
        if (lastConsoleWidth < 1 || lastConsoleHeight < 1) return false;

        root.Width = lastConsoleWidth;
        root.Height = lastConsoleHeight;
        return true;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new NoopDisposable();
        public void Dispose() { }
    }
}
