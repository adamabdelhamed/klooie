using PowerArgs;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKlooieTerminalHost : ITerminalHost
{
    private int width = 80;
    private int height = 25;
    private readonly BrowserConsoleProvider console;

    public BrowserKlooieTerminalHost(BrowserConsoleFrameBuffer frameBuffer)
    {
        FrameBuffer = frameBuffer;
        console = BrowserConsoleProvider.Current;
    }

    public BrowserConsoleFrameBuffer FrameBuffer { get; }

    public static void InitConsoleProvider() => ConsoleProvider.Current = new BrowserConsoleProvider(80, 25);

    public void Resize(int width, int height)
    {
        this.width = Math.Max(1, width);
        this.height = Math.Max(1, height);
        console.Resize(this.width, this.height);
    }

    public void EnqueueKey(ConsoleKeyInfo key) => console.EnqueueKey(key);

    public bool Present(LayoutRootPanel root, ConsoleBitmap bitmap)
    {
        FrameBuffer.CopyFrom(bitmap);
        return true;
    }

    public bool SyncSize(LayoutRootPanel root)
    {
        if (root.Width == width && root.Height == height) return false;
        root.ResizeTo(width, height);
        return true;
    }

    private sealed class BrowserConsoleProvider : IConsoleProvider
    {
        private readonly Queue<ConsoleKeyInfo> keys = new();

        public BrowserConsoleProvider(int width, int height)
        {
            BufferWidth = width;
            WindowWidth = width;
            WindowHeight = height;
            ForegroundColor = ConsoleString.DefaultForegroundColor;
            BackgroundColor = ConsoleString.DefaultBackgroundColor;
        }

        public static BrowserConsoleProvider Current => ConsoleProvider.Current as BrowserConsoleProvider
            ?? throw new InvalidOperationException("The browser console provider has not been initialized.");

        public RGB ForegroundColor { get; set; }
        public RGB BackgroundColor { get; set; }
        public bool KeyAvailable => keys.Count > 0;
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }
        public int BufferWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowWidth { get; set; }

        public void EnqueueKey(ConsoleKeyInfo key) => keys.Enqueue(key);

        public void Resize(int width, int height)
        {
            BufferWidth = width;
            WindowWidth = width;
            WindowHeight = height;
        }

        public void Append(string text) { }
        public void Clear() { }
        public int Read() => -1;
        public ConsoleKeyInfo ReadKey() => ReadKey(true);
        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            if (keys.TryDequeue(out var key)) return key;
            throw new InvalidOperationException("No browser key input is available.");
        }

        public string ReadLine() => string.Empty;
        public void Write(char[] buffer, int length) { }
        public void Write(object output) { }
        public void Write(in ConsoleCharacter consoleCharacter) { }
        public void Write(ConsoleString consoleString) { }
        public void WriteLine() { }
        public void WriteLine(object output) { }
        public void WriteLine(ConsoleString consoleString) { }
    }
}
