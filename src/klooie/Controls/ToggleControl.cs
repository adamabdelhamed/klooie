namespace klooie;

/// <summary>
/// A control that toggles between on and off
/// </summary>
public partial class ToggleControl : ProtectedConsolePanel
{
    private Label valueLabel;
    private Lifetime valueLifetime;
    /// <summary>
    /// Gets or sets the current On / Off value
    /// </summary>
    public partial bool On { get; set; }

    /// <summary>
    /// The On label text
    /// </summary>
    public string OnLabel { get; set; } = " On  ";

    /// <summary>
    /// The Off label text
    /// </summary>
    public string OffLabel { get; set; } = " Off ";

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
        CanFocus = true;
        Width = 10;
        Height = 1;
        valueLabel = ProtectedPanel.Add(new Label());
        Sync(nameof(On), async () => await Update(125), this);
        Sync(nameof(IsVisible), async () => await Update(0), this);
        Focused.Subscribe(async () => await Update(0), this);
        Unfocused.Subscribe(async () => await Update(0), this);
        KeyInputReceived.Subscribe(k => On = k.Key == ConsoleKey.Enter ? !On : On, this);
        Ready.SubscribeOnce(async () => await Update(0));
        OnColor = RGB.Magenta;
        OffColor = RGB.Gray;
        Foreground = RGB.Black;
        Background = RGB.DarkGray;
    }

    private async Task Update(float duration)
    {
        valueLifetime?.TryDispose();
        valueLifetime = new Lifetime();
        var newLeft = On ? Width - valueLabel.Width : 0;
        var newLabelBg = HasFocus ? FocusColor : On ? OnColor : OffColor;
        var newFg = HasFocus ? FocusContrastColor : Foreground;
        valueLabel.Text = On ? OnLabel.ToConsoleString(newFg, newLabelBg) : OffLabel.ToConsoleString(newFg, newLabelBg);

        if (Application == null)
        {
            valueLabel.X = newLeft;
            ProtectedPanel.Background = Background;
            return;
        }

        var dest = new RectF(newLeft, valueLabel.Top, valueLabel.Bounds.Width, valueLabel.Bounds.Height);
        await valueLabel.AnimateAsync(new ConsoleControlAnimationOptions()
        {
            IsCancelled = () => valueLifetime.IsExpired,
            Duration = duration,
            EasingFunction = EasingFunctions.EaseOutSoft,
            Destination = () => dest,
        });
    }
}
