namespace klooie.blazor.BrowserConsole;

public static class BrowserConsoleKeyMapper
{
    public static ConsoleKeyInfo? ToConsoleKeyInfo(BrowserKeyboardEvent keyboardEvent)
    {
        var key = keyboardEvent.Key;
        var code = keyboardEvent.Code;
        if (IsModifierOnlyKey(key)) return null;

        var consoleKey = MapConsoleKey(key, code, keyboardEvent.Location);
        if (consoleKey == ConsoleKey.NoName && key.Length != 1) return null;

        var keyChar = MapKeyChar(key, consoleKey, keyboardEvent);
        return new ConsoleKeyInfo(
            keyChar,
            consoleKey,
            keyboardEvent.ShiftKey,
            keyboardEvent.AltKey,
            keyboardEvent.CtrlKey);
    }

    private static bool IsModifierOnlyKey(string key) => key is
        "Alt" or
        "AltGraph" or
        "CapsLock" or
        "Control" or
        "Fn" or
        "FnLock" or
        "Hyper" or
        "Meta" or
        "NumLock" or
        "ScrollLock" or
        "Shift" or
        "Super" or
        "Symbol" or
        "SymbolLock";

    private static char MapKeyChar(string key, ConsoleKey consoleKey, BrowserKeyboardEvent keyboardEvent)
    {
        if (key.Length == 1)
        {
            var c = key[0];
            if (keyboardEvent.CtrlKey && !keyboardEvent.AltKey && char.IsAsciiLetter(c))
            {
                return (char)(char.ToUpperInvariant(c) - 'A' + 1);
            }

            return c;
        }

        return consoleKey switch
        {
            ConsoleKey.Backspace => '\b',
            ConsoleKey.Enter => '\r',
            ConsoleKey.Escape => '\x1b',
            ConsoleKey.Spacebar => ' ',
            ConsoleKey.Tab => '\t',
            _ => '\0',
        };
    }

    private static ConsoleKey MapConsoleKey(string key, string code, int location)
    {
        if (TryMapCode(code, out var consoleKey)) return consoleKey;
        if (TryMapKeyName(key, out consoleKey)) return consoleKey;

        if (key.Length == 1)
        {
            var c = key[0];
            if (char.IsAsciiLetter(c)) return (ConsoleKey)Enum.Parse(typeof(ConsoleKey), char.ToUpperInvariant(c).ToString());
            if (char.IsDigit(c)) return (ConsoleKey)Enum.Parse(typeof(ConsoleKey), "D" + c);
        }

        return ConsoleKey.NoName;
    }

    private static bool TryMapCode(string code, out ConsoleKey key)
    {
        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal) && char.IsAsciiLetter(code[3]))
        {
            key = (ConsoleKey)Enum.Parse(typeof(ConsoleKey), code[3].ToString());
            return true;
        }

        if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) && char.IsDigit(code[5]))
        {
            key = (ConsoleKey)Enum.Parse(typeof(ConsoleKey), "D" + code[5]);
            return true;
        }

        if (code.Length == 7 && code.StartsWith("Numpad", StringComparison.Ordinal) && char.IsDigit(code[6]))
        {
            key = (ConsoleKey)Enum.Parse(typeof(ConsoleKey), "NumPad" + code[6]);
            return true;
        }

        if (code.StartsWith("F", StringComparison.Ordinal) &&
            int.TryParse(code[1..], out var fNumber) &&
            fNumber is >= 1 and <= 24)
        {
            key = (ConsoleKey)Enum.Parse(typeof(ConsoleKey), "F" + fNumber);
            return true;
        }

        key = code switch
        {
            "ArrowDown" => ConsoleKey.DownArrow,
            "ArrowLeft" => ConsoleKey.LeftArrow,
            "ArrowRight" => ConsoleKey.RightArrow,
            "ArrowUp" => ConsoleKey.UpArrow,
            "Backquote" => ConsoleKey.Oem3,
            "Backslash" => ConsoleKey.Oem5,
            "Backspace" => ConsoleKey.Backspace,
            "BracketLeft" => ConsoleKey.Oem4,
            "BracketRight" => ConsoleKey.Oem6,
            "Comma" => ConsoleKey.OemComma,
            "ContextMenu" => ConsoleKey.Applications,
            "Delete" => ConsoleKey.Delete,
            "End" => ConsoleKey.End,
            "Enter" => ConsoleKey.Enter,
            "Equal" => ConsoleKey.OemPlus,
            "Escape" => ConsoleKey.Escape,
            "Help" => ConsoleKey.Help,
            "Home" => ConsoleKey.Home,
            "Insert" => ConsoleKey.Insert,
            "IntlBackslash" => ConsoleKey.Oem102,
            "Minus" => ConsoleKey.OemMinus,
            "NumpadAdd" => ConsoleKey.Add,
            "NumpadDecimal" => ConsoleKey.Decimal,
            "NumpadDivide" => ConsoleKey.Divide,
            "NumpadEnter" => ConsoleKey.Enter,
            "NumpadMultiply" => ConsoleKey.Multiply,
            "NumpadSubtract" => ConsoleKey.Subtract,
            "PageDown" => ConsoleKey.PageDown,
            "PageUp" => ConsoleKey.PageUp,
            "Pause" => ConsoleKey.Pause,
            "Period" => ConsoleKey.OemPeriod,
            "PrintScreen" => ConsoleKey.PrintScreen,
            "Quote" => ConsoleKey.Oem7,
            "Semicolon" => ConsoleKey.Oem1,
            "Slash" => ConsoleKey.Oem2,
            "Space" => ConsoleKey.Spacebar,
            "Tab" => ConsoleKey.Tab,
            _ => ConsoleKey.NoName,
        };

        return key != ConsoleKey.NoName;
    }

    private static bool TryMapKeyName(string browserKey, out ConsoleKey key)
    {
        key = browserKey switch
        {
            "ArrowDown" => ConsoleKey.DownArrow,
            "ArrowLeft" => ConsoleKey.LeftArrow,
            "ArrowRight" => ConsoleKey.RightArrow,
            "ArrowUp" => ConsoleKey.UpArrow,
            "Backspace" => ConsoleKey.Backspace,
            "Clear" => ConsoleKey.Clear,
            "ContextMenu" => ConsoleKey.Applications,
            "Delete" => ConsoleKey.Delete,
            "End" => ConsoleKey.End,
            "Enter" => ConsoleKey.Enter,
            "Escape" => ConsoleKey.Escape,
            "Help" => ConsoleKey.Help,
            "Home" => ConsoleKey.Home,
            "Insert" => ConsoleKey.Insert,
            "PageDown" => ConsoleKey.PageDown,
            "PageUp" => ConsoleKey.PageUp,
            "Pause" => ConsoleKey.Pause,
            "PrintScreen" => ConsoleKey.PrintScreen,
            " " => ConsoleKey.Spacebar,
            "Spacebar" => ConsoleKey.Spacebar,
            "Tab" => ConsoleKey.Tab,
            _ => ConsoleKey.NoName,
        };

        if (key != ConsoleKey.NoName) return true;

        if (browserKey.StartsWith("F", StringComparison.Ordinal) &&
            int.TryParse(browserKey[1..], out var fNumber) &&
            fNumber is >= 1 and <= 24)
        {
            key = (ConsoleKey)Enum.Parse(typeof(ConsoleKey), "F" + fNumber);
            return true;
        }

        return false;
    }
}
