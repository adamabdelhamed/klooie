namespace klooie.tests;
internal class KlooieTestConsole : IConsoleProvider
{
    public ConsoleColor ForegroundColor { get; set; }
    public ConsoleColor BackgroundColor { get; set; }
    public bool KeyAvailable => false;
    public void Append(string text) { }
    public void Clear() { }
    public int CursorLeft { get; set; }
    public int CursorTop { get; set; }
    public int BufferWidth { get; set; }
    public int WindowHeight { get; set; }
    public int WindowWidth { get; set; }
    public void Write(char[] buffer, int length) { }
    public void Write(object output) { }
    public void WriteLine(object output) { }
    public void WriteLine()  { }
    public void Write(ConsoleString consoleString) { }
    public void Write(in ConsoleCharacter consoleCharacter) { }
    public void WriteLine(ConsoleString consoleString) { }
    public ConsoleKeyInfo ReadKey() => throw new NotImplementedException();
    public int Read() => throw new NotImplementedException();
    public ConsoleKeyInfo ReadKey(bool intercept) => throw new NotImplementedException();
    public string ReadLine() => throw new NotImplementedException();
}