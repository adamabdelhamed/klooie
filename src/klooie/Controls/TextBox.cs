namespace klooie;
/// <summary>
/// A control that lets the user provide text input
/// </summary>
public partial class TextBox : ConsoleControl
{
    private static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(500);
    private ConsoleApp.SetIntervalHandle blinkTimerHandle;
    private bool isAllSelected;
    private bool isBlinking;

    /// <summary>
    /// Gets the editor object that controls the rich text capabilities of the text box
    /// </summary>
    public RichTextEditor Editor { get; private set; }

    /// <summary>
    /// If true then the entire text is shown as selected on focus. Otherwise, the cursor
    /// will be placed at the end of the text.
    /// </summary>
    public bool SelectAllOnFocus { get; set; } = false;

    /// <summary>
    /// Gets or sets the value in the text box
    /// </summary>
    public partial ConsoleString Value { get; set; }

    /// <summary>
    /// Set to true to block input while continuing to allow focus.
    /// </summary>
    public bool IsInputBlocked { get; set; }

    /// <summary>
    /// Creates a new text box
    /// </summary>
    public TextBox()
    {
        Value = ConsoleString.Empty;
        FocusColor = DefaultColors.FocusColor;
        this.Editor = new RichTextEditor();
        this.Height = 1;
        this.Width = 15;
        CanFocus = true;
        this.Focused.Subscribe(TextBox_Focused, this);
        this.Unfocused.Subscribe(TextBox_Unfocused, this);
        KeyInputReceived.Subscribe(OnKeyInputReceived, this);
        Subscribe(nameof(Value), () =>
        {
            if (Value == null) throw new ArgumentNullException(nameof(Value));
            Editor.CurrentValue = Value;
            Editor.CursorPosition = Value.Length;
        }, this);
    }

    private void TextBox_Focused()
    {
        if (SelectAllOnFocus && Value.Length > 0)
        {
            isAllSelected = true;
            blinkTimerHandle?.Dispose();
            isBlinking = false;
        }
        else
        {
            StartBlinking();
        }
    }

    private void StartBlinking()
    {
        isBlinking = true;
        blinkTimerHandle = Application.SetInterval(() =>
        {
            if (HasFocus == false) return;
            isBlinking = !isBlinking;
            Application.RequestPaint();
        }, BlinkInterval);
    }

    private void TextBox_Unfocused()
    {
        blinkTimerHandle?.Dispose();
        isBlinking = false;
        isAllSelected = false;
    }

    private void OnKeyInputReceived(ConsoleKeyInfo info)
    {
        if (IsInputBlocked) return;

        if (isAllSelected && info.Key == ConsoleKey.Backspace)
        {
            Value = ConsoleString.Empty;
            StartBlinking();
            return;
        }
        else if (isAllSelected && info.Key == ConsoleKey.LeftArrow)
        {
            isAllSelected = false;
            Editor.CursorPosition = 0;
            StartBlinking();
            return;
        }
        else if (isAllSelected && info.Key == ConsoleKey.RightArrow)
        {
            isAllSelected = false;
            Editor.CursorPosition = Value.Length;
            StartBlinking();
            return;
        }
        else if (isAllSelected)
        {
            isAllSelected = false;
            ConsoleCharacter? p = this.Value.Length == 0 ? null : this.Value[this.Value.Length - 1];
            var c = new ConsoleCharacter(info.KeyChar,
                p.HasValue ? p.Value.ForegroundColor : ConsoleString.DefaultForegroundColor,
                p.HasValue ? p.Value.BackgroundColor : ConsoleString.DefaultBackgroundColor);

            Value = RichTextCommandLineReader.IsWriteable(info) ? new ConsoleString(new ConsoleCharacter[] { c }) : ConsoleString.Empty;
            StartBlinking();
            return;
        }

        ConsoleCharacter? prototype = this.Value.Length == 0 ? null : this.Value[this.Value.Length - 1];
        Editor.RegisterKeyPress(info, prototype);
        Value = Editor.CurrentValue;
        isBlinking = true;
        Application.ChangeInterval(blinkTimerHandle, BlinkInterval);
    }

    /// <summary>
    /// paints the text box
    /// </summary>
    /// <param name="context"></param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        var toPaint = Editor.CurrentValue;

        var offset = 0;
        if (toPaint.Length >= Width && Editor.CursorPosition > Width - 1)
        {
            offset = (Editor.CursorPosition + 1) - Width;
            toPaint = toPaint.Substring(offset);
        }

        var bgTransformed = new List<ConsoleCharacter>();

        foreach (var c in toPaint)
        {
            if (isAllSelected)
            {
                bgTransformed.Add(new ConsoleCharacter(c.Value, RGB.Black, FocusColor));
            }
            else if (c.BackgroundColor == ConsoleString.DefaultBackgroundColor && Background != ConsoleString.DefaultBackgroundColor)
            {
                bgTransformed.Add(new ConsoleCharacter(c.Value, Foreground, Background));
            }
            else
            {
                bgTransformed.Add(c);
            }
        }

        context.DrawString(new ConsoleString(bgTransformed), 0, 0);

        if (isBlinking)
        {
            char blinkChar = Editor.CursorPosition >= toPaint.Length ? ' ' : toPaint[Editor.CursorPosition].Value;
            var pen = new ConsoleCharacter(blinkChar, DefaultColors.FocusContrastColor, FocusColor);
            context.DrawPoint(pen, Editor.CursorPosition - offset, 0);
        }
    }
}
