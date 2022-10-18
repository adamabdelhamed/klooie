using System.Text.RegularExpressions;
namespace klooie;
/// <summary>
/// A class that represents a keyboard shortcut that can be activate a control that does not have focus
/// </summary>
public class KeyboardShortcut
{
    /// <summary>
    /// The shortcut key
    /// </summary>
    public ConsoleKey Key { get; set; }

    /// <summary>
    /// A key modifier (e.g. shift, alt) that, when present, must be pressed in order for the shortcut key to trigger.  Note that control is not
    /// supported because it doesn't play well in a console
    /// </summary>
    public ConsoleModifiers? Modifier { get; set; }

    /// <summary>
    /// Creates a new shortut
    /// </summary>
    /// <param name="key">the shortcut key</param>
    /// <param name="modifier">A key modifier (e.g. shift, alt) that, when present, must be pressed in order for the shortcut key to trigger.  Note that control is not
    /// supported because it doesn't play well in a console</param>
    public KeyboardShortcut(ConsoleKey key, ConsoleModifiers? modifier = null)
    {
        if (modifier == ConsoleModifiers.Control) throw new InvalidOperationException("Control is not supported as a keyboard shortcut modifier");
        this.Key = key;
        this.Modifier = modifier;
    }
}

/// <summary>
/// A button control that can be 'pressed' by the user
/// </summary>
public class Button : ConsoleControl
{
    private KeyboardShortcut shortcut;
    private ConsoleString display;
    /// <summary>
    /// An event that fires when the button is clicked
    /// </summary>
    public Event Pressed { get; private init; } = new Event();

    /// <summary>
    /// Gets or sets the text that is displayed on the button
    /// </summary>
    public ConsoleString? Text { get => Get<ConsoleString>(); set => Set(value); }

    /// <summary>
    /// Creates a new button control
    /// <param name="shortcut">An optional keyboard shortcut that can be used to press the button</param>
    /// </summary>
    public Button(KeyboardShortcut? shortcut = null)
    {
        this.shortcut = shortcut;
        Height = 1;
        this.Subscribe(nameof(Text), UpdateText, this);
        this.Subscribe(nameof(Foreground),UpdateText, this);
        this.Subscribe(nameof(Background),UpdateText, this);
        this.Focused.Subscribe(UpdateText, this);
        this.Unfocused.Subscribe(UpdateText, this);
        this.AddedToVisualTree.Subscribe(OnAddedToVisualTree, this);
        this.KeyInputReceived.Subscribe(OnKeyInputReceived, this);
    }

    private void UpdateText()
    {
        display = GetButtonDisplayString();
        Width = display.Length;
    }

    private ConsoleString GetButtonDisplayString()
    {
        var anchorFg = HasFocus ? FocusContrastColor : CanFocus ? Foreground: Foreground.Darker;
        var anchorBg = HasFocus ? FocusColor : Background;
        var startAnchor = "[".ToConsoleString(anchorFg, anchorBg);
        var endAnchor = "]".ToConsoleString(anchorFg, anchorBg);
        var ret = startAnchor + GetEffectiveLabelText() + GetKeyString() + endAnchor;
        return ret;
    }

    private ConsoleString GetEffectiveLabelText()
    {
        var effectiveText = Text ?? ConsoleString.Empty;
        if (effectiveText.IsUnstyled == false) return effectiveText;

        var fg = CanFocus ? Foreground : DefaultColors.DisabledColor;
        effectiveText = new ConsoleString(effectiveText.StringValue, fg, Background);
        return effectiveText;
    }

    private ConsoleString GetKeyString()
    {
        if (shortcut == null) return ConsoleString.Empty;
        
        var keyString = shortcut.Key.ToString();
        var isFromTopNumberBar = Regex.IsMatch(keyString, @"D\d");
        var isFromNumPad = keyString.StartsWith("NumPad");
        keyString = isFromTopNumberBar ? keyString.Substring(1) : isFromNumPad ? keyString.Substring("NumPad".Length) : keyString;
        var isAlt = shortcut.Modifier.HasValue && shortcut.Modifier == ConsoleModifiers.Alt;
        var isShift = shortcut.Modifier.HasValue && shortcut.Modifier == ConsoleModifiers.Shift;
        var altStr = new ConsoleString($" (ALT+{keyString})", CanFocus ? Foreground : DefaultColors.DisabledColor, Background);
        var shiftStr = new ConsoleString($" (SHIFT+{keyString})", CanFocus ? Foreground : DefaultColors.DisabledColor, Background);
        var nakedStr = new ConsoleString($" ({keyString})", CanFocus ? Foreground : DefaultColors.DisabledColor, Background);
        var ret = isAlt ? altStr : isShift ? shiftStr : nakedStr;
        return ret;
    }

    private void OnAddedToVisualTree()
    {
        if (shortcut == null) return;
        {
            Application.PushKeyForLifetime(shortcut.Key, shortcut.Modifier, PressIfCanFocus, this);

            if (Regex.IsMatch(shortcut.Key.ToString(), @"D\d"))
            {
                var num = shortcut.Key.ToString().Last();
                Application.PushKeyForLifetime((ConsoleKey)Enum.Parse(typeof(ConsoleKey), "NumPad" + num), shortcut.Modifier, PressIfCanFocus, this);
            }
            else if (shortcut.Key.ToString().StartsWith("NumPad"))
            {
                var num = shortcut.Key.ToString().Last();
                Application.PushKeyForLifetime((ConsoleKey)Enum.Parse(typeof(ConsoleKey), "D" + num), shortcut.Modifier, PressIfCanFocus, this);
            }
        }
    }

    private void OnKeyInputReceived(ConsoleKeyInfo info) 
    {
        if (info.Key == ConsoleKey.Enter) PressIfCanFocus(); 
    }
    private void PressIfCanFocus()
    {
        if (this.CanFocus == false) return;
        Pressed.Fire();
    }

    /// <summary>
    /// paints the button
    /// </summary>
    /// <param name="context">drawing context</param>
    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(display, 0, 0);
}
