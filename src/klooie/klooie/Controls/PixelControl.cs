namespace klooie;

/// <summary>
/// A control that renders a single pixel
/// </summary>
public class PixelControl : ConsoleControl
{
    private ConsoleCharacter _value;
    /// <summary>
    /// Gets or sets the character value to be displayed
    /// </summary>
    public ConsoleCharacter Value { get => _value; set => SetHardIf(ref _value, value, value != _value); }
    /// <summary>
    /// Creates a new pixel control
    /// </summary>
    public PixelControl()
    {
        ResizeTo(1, 1);
        Subscribe(nameof(Bounds), EnsureNoResize, this);
        Value = new ConsoleCharacter(' ', Foreground, Background);
    }

    private void EnsureNoResize()
    {
        if (Width != 1 || Height != 1) throw new InvalidOperationException(nameof(PixelControl) + " must be 1 X 1");
    }

    protected override void OnPaint(ConsoleBitmap context) => context.Pixels[0][0] = Value;
}
