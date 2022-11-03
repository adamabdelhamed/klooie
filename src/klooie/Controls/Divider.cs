namespace klooie;

/// <summary>
/// A control that can be used to divide panels
/// </summary>
public class Divider : ConsoleControl
{
    public Orientation Orientation { get; private init; }

    /// <summary>
    /// Creates a divider
    /// </summary>
    public Divider(Orientation orientation)
    {
        CanFocus = false;
        this.Orientation = orientation;

        var limitedPropName = Orientation == Orientation.Vertical ? nameof(Width) : nameof(Height);
        Func<int> limitedPropValue = Orientation == Orientation.Vertical ? () => Width : () => Height;
        Func<int> limitedPropSetter = Orientation == Orientation.Vertical ? () => Width = 1 : () => Height = 1;
        limitedPropSetter();
        Subscribe(nameof(Bounds), () => ThrowArgumentExceptionIf(limitedPropName, limitedPropValue() != 1, $"{limitedPropName} must equal 1 for a {Orientation} divider"), this);
    }
    
    private void ThrowArgumentExceptionIf(string propertyName, bool condition, string message)
    {
        if (condition == false) return;
        throw new ArgumentException(message, propertyName);
    }

    /// <summary>
    /// Paints the divider
    /// </summary>
    /// <param name="context">the bitmap target</param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        var c = Orientation == Orientation.Vertical ? '|' : '-';
        context.Fill(new ConsoleCharacter(c, HasFocus ? FocusContrastColor : Foreground, HasFocus ? FocusColor : Background));
    }
}
