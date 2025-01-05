namespace klooie;

/// <summary>
/// A control that displays a single line of text
/// </summary>
public partial class Label : ConsoleControl
{
    private bool autoSize;
    private ConsoleString _cleaned;
    public bool EnableCharacterByCharacterStyleDetection { get; set; }
    /// <summary>
    /// The text to display
    /// </summary>
    public partial ConsoleString Text { get; set; }


    public Label() : this(string.Empty,true) { }

    /// <summary>
    /// Creates a new label
    /// </summary>
    /// <param name="initialText">the initial text value</param>
    /// <param name="autoSize">true to auto size the width of the label, false otherwise</param>
    public Label(string initialText, bool autoSize = true) : this(initialText?.ToConsoleString(), autoSize)
    {

    }
    /// <summary>
    /// Creates a new label
    /// </summary>
    /// <param name="initialText">the initial text value</param>
    /// <param name="autoSize">true to auto size the width of the label, false otherwise</param>
    public Label(ConsoleString? initialText = null, bool autoSize = true)
    {
        this.autoSize = autoSize;
        Text = initialText ?? ConsoleString.Empty;
        CanFocus = false;

        TextChanged.Sync(OnTextChanged, this);
        ForegroundChanged.Subscribe(NormalizeNewlinesTabsAndStyleText, this);
        BackgroundChanged.Subscribe(NormalizeNewlinesTabsAndStyleText, this);
        //BoundsChanged.Subscribe(NormalizeNewlinesTabsAndStyleText, this);
        Focused.Subscribe(FocusChanged, this);
        Unfocused.Subscribe(FocusChanged, this);
    }

    private void FocusChanged()
    {
        NormalizeNewlinesTabsAndStyleText();
        TextChanged.Fire();
    }

    private void OnTextChanged()
    {
        if (Text == null) throw new ArgumentNullException(nameof(Text));
        NormalizeNewlinesTabsAndStyleText();
        Width = autoSize ? _text.Length : Width;
    }

    private void NormalizeNewlinesTabsAndStyleText() => _cleaned = EnableCharacterByCharacterStyleDetection ?
        TextCleaner.NormalizeNewlinesTabsAndStyleV2(_text, HasFocus ? FocusContrastColor : Foreground, HasFocus ? FocusColor : Background) :
        TextCleaner.NormalizeNewlinesTabsAndStyle(_text, HasFocus ? FocusContrastColor : Foreground, HasFocus ? FocusColor : Background);
    public override string ToString() => $"Label: " + Text;
    protected override void OnPaint(ConsoleBitmap context)
    {
        context.DrawString(_cleaned, 0, 0);
    }
}

 