using PowerArgs;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKlooieTerminalHost : ITerminalHost
{
    public const int TerminalWidth = 120;
    public const int TerminalHeight = 16;

    public BrowserKlooieTerminalHost(BrowserConsoleBitmap bitmap)
    {
        Bitmap = bitmap;
    }

    public BrowserConsoleBitmap Bitmap { get; }

    public static void InitConsoleProvider() => ConsoleProvider.Current = new NoOpConsole(TerminalWidth, TerminalHeight);

    public bool Present(LayoutRootPanel root, ConsoleBitmap bitmap)
    {
        Bitmap.CopyFrom(bitmap);
        return true;
    }

    public bool SyncSize(LayoutRootPanel root)
    {
        if (root.Width == TerminalWidth && root.Height == TerminalHeight) return false;
        root.ResizeTo(TerminalWidth, TerminalHeight);
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
