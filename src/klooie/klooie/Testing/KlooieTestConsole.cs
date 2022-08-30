using PowerArgs;
using System;
using System.Threading;

namespace klooie.tests;
public class KlooieTestConsole : IConsoleProvider
{
    public event Action<string> WriteHappened;
    public event Action ClearHappened;

    public ConsoleColor ForegroundColor { get; set; }

    public ConsoleColor BackgroundColor { get; set; }

    public static KlooieTestConsole SimulateConsoleInput(string input)
    {
        var simulator = new KlooieTestConsole(input);
        ConsoleProvider.Current = simulator;
        return simulator;
    }

    string input;
    int i;

    public bool KeyAvailable
    {
        get
        {
            return i < input.Length;
        }
    }

    public KlooieTestConsole(string input = "")
    {
        this.input = input;
        i = 0;
        BufferWidth = 80;
        WindowWidth = 80;
        WindowHeight = 50;
    }

    public void Append(string text)
    {
        input = input + text;
    }

    public void Clear()
    {
        if (ClearHappened != null) ClearHappened();
    }

    public int CursorLeft { get; set; }
    public int CursorTop { get; set; }
    public int BufferWidth { get; set; }
    public int WindowHeight { get; set; }
    public int WindowWidth { get; set; }

    bool shift = false;
    bool control = false;
    public ConsoleKeyInfo ReadKey()
    {
        if (i == input.Length) return new ConsoleKeyInfo((char)0, ConsoleKey.Enter, false, false, false);
        var c = input[i++];
        ConsoleKey key = ConsoleKey.NoName;

        if (c == '\b') key = ConsoleKey.Backspace;
        else if (c == ' ') key = ConsoleKey.Spacebar;
        else if (c == '\t') key = ConsoleKey.Tab;
        else if (c == '{' && ReadAheadLookFor("delete}")) key = ConsoleKey.Delete;
        else if (c == '{' && ReadAheadLookFor("home}")) key = ConsoleKey.Home;
        else if (c == '{' && ReadAheadLookFor("end}")) key = ConsoleKey.End;
        else if (c == '{' && ReadAheadLookFor("escape}")) key = ConsoleKey.Escape;
        else if (c == '{' && ReadAheadLookFor("left}")) key = ConsoleKey.LeftArrow;
        else if (c == '{' && ReadAheadLookFor("right}")) key = ConsoleKey.RightArrow;
        else if (c == '{' && ReadAheadLookFor("up}")) key = ConsoleKey.UpArrow;
        else if (c == '{' && ReadAheadLookFor("down}")) key = ConsoleKey.DownArrow;
        else if (c == '{' && ReadAheadLookFor("enter}")) key = ConsoleKey.Enter;
        else if (c == '{' && ReadAheadLookFor("wait}"))
        {
            Thread.Sleep(1000);
            return ReadKey();
        }
        else if (c == '{' && ReadAheadLookFor("w}"))
        {
            Thread.Sleep(100);
            return ReadKey();
        }
        else if (c == '{' && ReadAheadLookFor("shift}"))
        {
            shift = true;
            var ret = ReadKey();
            shift = false;
            return ret;
        }
        else if (c == '{' && ReadAheadLookFor("control}"))
        {
            control = true;
            var ret = ReadKey();
            control = false;
            return ret;
        }

        return new ConsoleKeyInfo(c, key, shift, false, control);
    }

    private bool ReadAheadLookFor(string toFind)
    {
        int k = 0;
        for (int j = i; j < i + toFind.Length; j++)
        {
            if (input[j] != toFind[k++])
            {
                return false;
            }
        }
        i += toFind.Length;
        return true;
    }


    public void Write(char[] buffer, int length)
    {
        var str = new string(buffer, 0, length);
        Write(str);
    }

    public void Write(object output)
    {
        string text = output == null ? "" : output.ToString();
        CursorLeft += text.Length;

        if (WriteHappened != null)
        {
            WriteHappened(text);
        }
    }
    public void WriteLine(object output)
    {
        if (WriteHappened != null)
        {
            string text = output == null ? "" : output.ToString();
            WriteHappened(text);
        }

        CursorLeft = 0;
        CursorTop++;
    }
    public void WriteLine()
    {
        if (WriteHappened != null)
        {
            WriteHappened(Environment.NewLine);
        }

        CursorLeft = 0;
        CursorTop++;
    }


    public int Read()
    {
        throw new NotImplementedException();
    }

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        return ReadKey();
    }

    public string ReadLine()
    {
        throw new NotImplementedException();
    }


    public void Write(ConsoleString consoleString)
    {
        Write(consoleString.ToString());
    }

    public void Write(in ConsoleCharacter consoleCharacter)
    {
        Write(consoleCharacter.ToString());
    }

    public void WriteLine(ConsoleString consoleString)
    {
        WriteLine(consoleString.ToString());
    }
}