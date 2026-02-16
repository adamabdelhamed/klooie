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

        ConsoleProvider.Current.Clear();

        var lastSyncTime = DateTime.UtcNow;
        while (DateTime.UtcNow - lastSyncTime <= TimeSpan.FromSeconds(.25f))
        {
            if (ConsoleProvider.Current.BufferWidth != lastConsoleWidth || ConsoleProvider.Current.WindowHeight != lastConsoleHeight)
            {
                lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
                lastConsoleHeight = ConsoleProvider.Current.WindowHeight;
                lastSyncTime = DateTime.UtcNow;
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
