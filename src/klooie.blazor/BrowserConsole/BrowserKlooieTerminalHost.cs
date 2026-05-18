using PowerArgs;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKlooieTerminalHost : ITerminalHost
{
    private int width = 80;
    private int height = 25;

    public BrowserKlooieTerminalHost(BrowserConsoleFrameBuffer frameBuffer)
    {
        FrameBuffer = frameBuffer;
    }

    public BrowserConsoleFrameBuffer FrameBuffer { get; }

    public static void InitConsoleProvider() => ConsoleProvider.Current = new NoOpConsole(80, 25);

    public void Resize(int width, int height)
    {
        this.width = Math.Max(1, width);
        this.height = Math.Max(1, height);
        if (ConsoleProvider.Current is NoOpConsole console) console.Resize(this.width, this.height);
    }

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

    private sealed class NoOpConsole : IConsoleProvider
    {
        public NoOpConsole(int width, int height)
        {
            BufferWidth = width;
            WindowWidth = width;
            WindowHeight = height;
            ForegroundColor = ConsoleString.DefaultForegroundColor;
            BackgroundColor = ConsoleString.DefaultBackgroundColor;
        }

        public RGB ForegroundColor { get; set; }
        public RGB BackgroundColor { get; set; }
        public bool KeyAvailable => false;
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }
        public int BufferWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowWidth { get; set; }

        public void Resize(int width, int height)
        {
            BufferWidth = width;
            WindowWidth = width;
            WindowHeight = height;
        }

        public void Append(string text) { }
        public void Clear() { }
        public int Read() => -1;
        public ConsoleKeyInfo ReadKey() => throw new NotSupportedException();
        public ConsoleKeyInfo ReadKey(bool intercept) => throw new NotSupportedException();
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
