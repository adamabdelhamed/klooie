namespace klooie;

/// <summary>
/// A control that renders multi-line text and supports text wrapping
/// </summary>
public class TextViewer : ConsoleControl
{
    public enum AutoSizeMode
    {
        /// <summary>
        /// Use this option if you would like to size the control yourself
        /// </summary>
        None,
        /// <summary>
        /// Use this option if you want the control to auto size the height.
        /// You are still responsible for setting the width.
        /// </summary>
        Height,
    }
    private ConsoleString _text;
    private ConsoleString cleaned;
    private List<List<ConsoleCharacter>> lines;
    private Tokenizer<Token> tokenizer;

    /// <summary>
    /// Gets the auto size mode that was set in the constructor
    /// </summary>
    public AutoSizeMode AutoSize { get; private init; }

    /// <summary>
    /// Gets or sets the text displayed in the viewer
    /// </summary>
    public ConsoleString Text
    { 
        get => _text;
        set 
        {
            if (value == null) throw new ArgumentNullException(nameof(Text));
            if (SetHardIf(ref _text, value, _text != value) == false) return;
        }
    }


    /// <summary>
    /// Gets or sets the max height. 
    /// </summary>
    public int? MaxHeight { get { return Get<int?>(); } set { Set(value); } }

    /// <summary>
    /// Creates a new TextViewer
    /// </summary>
    /// <param name="initialText">the initial text value</param>
    /// <param name="autoSize">sets your auto sizeing preference</param>
    public TextViewer(ConsoleString initialText = null, AutoSizeMode autoSize = AutoSizeMode.Height)
    {
        CanFocus = false;
        lines = new List<List<ConsoleCharacter>>();
        Text = initialText ?? ConsoleString.Empty;
        this.AutoSize = autoSize;
        Subscribe(nameof(Text), RefreshLines, this);
        Subscribe(nameof(MaxHeight), RefreshLines, this);
        Subscribe(nameof(Bounds), RefreshLines, this);
        Subscribe(nameof(Foreground), RefreshLines, this);
        Subscribe(nameof(Background), RefreshLines, this);
    }

    private void RefreshLines()
    {
        cleaned = TextCleaner.NormalizeNewlinesTabsAndStyle(_text, Foreground, Background);
        this.lines.Clear();
        List<ConsoleCharacter> currentLine = null;
        SmartWrapNewLine(lines, ref currentLine);
        var cleanedString = cleaned.ToString();

        if (tokenizer == null)
        {
            tokenizer = new Tokenizer<Token>();
            tokenizer.Delimiters.Add(".");
            tokenizer.Delimiters.Add("?");
            tokenizer.Delimiters.Add("!");
            tokenizer.WhitespaceBehavior = WhitespaceBehavior.DelimitAndInclude;
            tokenizer.DoubleQuoteBehavior = DoubleQuoteBehavior.NoSpecialHandling;
        }

        var tokens = tokenizer.Tokenize(cleanedString);

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.Value == "\n")
            {
                SmartWrapNewLine(lines, ref currentLine);
            }
            else if (currentLine.Count + token.Value.Length <= Width)
            {
                currentLine.AddRange(cleaned.Substring(token.StartIndex, token.Value.Length));
            }
            else
            {
                SmartWrapNewLine(lines, ref currentLine);
                var toAdd = cleaned.Substring(token.StartIndex, token.Value.Length).TrimStart();
                foreach (var c in toAdd)
                {
                    if (currentLine.Count == Width)
                    {
                        SmartWrapNewLine(lines, ref currentLine);
                    }
                    currentLine.Add(c);
                }
            }
        }

        
        Height = AutoSize == AutoSizeMode.None ? Height : MaxHeight.HasValue ? Math.Min(lines.Count, MaxHeight.Value) : lines.Count;
    }

    private void SmartWrapNewLine(List<List<ConsoleCharacter>> lines, ref List<ConsoleCharacter> currentLine)
    {
        currentLine = new List<ConsoleCharacter>();
        lines.Add(currentLine);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        var maxY = Math.Min(Height, lines.Count);
        for (int y = 0; y < maxY; y++)
        {
            var line = lines[y];
            var maxX = Math.Min(line.Count, Width);
            for (int x = 0; x < maxX; x++)
            {
                context.DrawPoint(HasFocus ? new ConsoleCharacter(line[x].Value, DefaultColors.FocusContrastColor, DefaultColors.FocusColor) : line[x], x, y);
            }
        }
    }
}
