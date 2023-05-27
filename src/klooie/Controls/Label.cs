namespace klooie;

/// <summary>
/// A control that displays a single line of text
/// </summary>
public class Label : ConsoleControl
{
    private bool autoSize;
    private ConsoleString _text;
    private ConsoleString _cleaned;
    public bool EnableCharacterByCharacterStyleDetection { get; set; }
    /// <summary>
    /// The text to display
    /// </summary>
    public ConsoleString Text { get => _text; set => TextChanged(value); }

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
        Subscribe(nameof(Text), NormalizeNewlinesTabsAndStyleText, this);
        Subscribe(nameof(Bounds), NormalizeNewlinesTabsAndStyleText, this);
        Subscribe(nameof(Foreground), NormalizeNewlinesTabsAndStyleText, this);
        Subscribe(nameof(Background), NormalizeNewlinesTabsAndStyleText, this);
        Focused.Subscribe(FocusChanged, this);
        Unfocused.Subscribe(FocusChanged, this);
    }

    private void FocusChanged()
    {
        NormalizeNewlinesTabsAndStyleText();
        FirePropertyChanged(nameof(Text));
    }

    private void TextChanged(ConsoleString value)
    {
        if (value == null) throw new ArgumentNullException(nameof(Text));
        if (_text == value) return;
        _text = value;
        NormalizeNewlinesTabsAndStyleText();
        FirePropertyChanged(nameof(Text));
        Width = autoSize ? _text.Length : Width;
    }

    private void NormalizeNewlinesTabsAndStyleText() => _cleaned = EnableCharacterByCharacterStyleDetection ?
        TextCleaner.NormalizeNewlinesTabsAndStyleV2(_text, HasFocus ? FocusContrastColor : Foreground, HasFocus ? FocusColor : Background) :
        TextCleaner.NormalizeNewlinesTabsAndStyle(_text, HasFocus ? FocusContrastColor : Foreground, HasFocus ? FocusColor : Background);
    public override string ToString() => $"Label: " + Text;
    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(_cleaned, 0, 0);
}
