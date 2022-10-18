namespace klooie;

internal static class TextCleaner
{
    public static ConsoleString NormalizeNewlinesTabsAndStyle(ConsoleString str, RGB fg, RGB bg)
    {
        var buffer = new List<ConsoleCharacter>();
        var isUnstyled = str.IsUnstyled;
        for(var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (ProcessTab(buffer, c, isUnstyled,fg,bg)) continue;
            ConsoleCharacter? next = i < str.Length - 1 ? str[i + 1] : null;
            if (ProcessNewLine(buffer, c, next, isUnstyled, fg, bg)) continue;

            var fgC = isUnstyled ? fg : c.ForegroundColor;
            var bgC = isUnstyled ? bg : c.BackgroundColor;

            buffer.Add(new ConsoleCharacter(c.Value, fgC, bgC));

        }
        var ret = new ConsoleString(buffer);
        return ret;
    }

    private static bool ProcessNewLine(List<ConsoleCharacter> buffer, ConsoleCharacter c, ConsoleCharacter? next, bool isUnstyled, RGB fg, RGB bg)
    {
        if (c.Value != '\r') return false;

        // just don't add the \r since the \n is next
        if (next.HasValue && next.Value.Value == '\n') return true;

        fg = isUnstyled ? fg : c.ForegroundColor;
        bg = isUnstyled ? bg : c.BackgroundColor;

        // replace \r with \n
        buffer.Add(new ConsoleCharacter('\n', fg, bg));
        return true;
    }

    private static bool ProcessTab(List<ConsoleCharacter> buffer, ConsoleCharacter c, bool isUnstyled, RGB fg, RGB bg)
    {
        if (c.Value != '\t') return false;

        fg = isUnstyled ? fg : c.ForegroundColor;
        bg = isUnstyled ? bg : c.BackgroundColor;
        for (var j = 0; j < 4; j++)
        {
            buffer.Add(new ConsoleCharacter(' ', fg, bg));
        }
        return true;
    }
}
