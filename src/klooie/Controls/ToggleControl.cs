namespace klooie;

/// <summary>
/// A control that toggles between on and off
/// </summary>
public partial class ToggleControl : ProtectedConsolePanel
{
    private Label valueLabel;
    /// <summary>
    /// Gets or sets the current On / Off value
    /// </summary>
    public partial bool On { get; set; }

    /// <summary>
    /// The On label text
    /// </summary>
    public partial string OnLabel { get; set; } 

    /// <summary>
    /// The Off label text
    /// </summary>
    public partial string OffLabel { get; set; } 

    /// <summary>
    /// The On color
    /// </summary>
    public partial RGB OnColor { get; set; }

    /// <summary>
    /// The Off color
    /// </summary>
    public partial RGB OffColor { get; set; }

    /// <summary>
    /// Creates a new ToggleControl
    /// </summary>
    public ToggleControl()
    {
        OnLabel = " On  ";
        OffLabel = " Off ";
        CanFocus = true;
        Width = 10;
        Height = 1;
        valueLabel = ProtectedPanel.Add(new Label());
        OnChanged.Sync(Update, this);
        IsVisibleChanged.Sync(Update, this);
        OnLabelChanged.Subscribe(Update, this);
        OffLabelChanged.Subscribe(Update, this);
        Focused.Subscribe(Update, this);
        Unfocused.Subscribe(Update, this);
        KeyInputReceived.Subscribe(k => On = k.Key == ConsoleKey.Enter ? !On : On, this);
        Ready.SubscribeOnce(Update);
        OnColor = RGB.Magenta;
        OffColor = RGB.Gray;
        Foreground = RGB.Black;
        Background = RGB.DarkGray;
    }

    private void Update()
    {
        var newLeft = On ? Width - valueLabel.Width : 0;
        var newLabelBg = HasFocus ? FocusColor : On ? OnColor : OffColor;
        var newFg = HasFocus ? FocusContrastColor : Foreground;
        valueLabel.Text = On ? OnLabel.ToConsoleString(newFg, newLabelBg) : OffLabel.ToConsoleString(newFg, newLabelBg);
        var dest = new RectF(newLeft, valueLabel.Top, valueLabel.Bounds.Width, valueLabel.Bounds.Height);
        valueLabel.Bounds = dest;
    }
}
