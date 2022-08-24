namespace klooie;
/// <summary>
/// A ConsolePanel that can prevent outside influences from
/// adding to its Controls collection. You must use the internal
/// Unlock method to add or remove controls.
/// </summary>
public class ProtectedConsolePanel : Container
{
    /// <summary>
    /// Derived classes can access the protected panel and be sure that
    /// callers cannot access the panel directly.
    /// </summary>
    protected ConsolePanel ProtectedPanel { get; private set; }

    /// <summary>
    /// internal so that ConsoleApp can poke in
    /// </summary>
    internal ConsolePanel ProtectedPanelInternal => ProtectedPanel;

    /// <summary>
    /// Gets the children of this control
    /// </summary>
    public override IEnumerable<ConsoleControl> Children => ProtectedPanel.Children;

    /// <summary>
    /// Creates a new ConsolePanel
    /// </summary>
    public ProtectedConsolePanel()
    {
        this.CanFocus = false;
        ProtectedPanel = new ConsolePanel();
        ProtectedPanel.Parent = this;
        ProtectedPanel.Fill();
        this.SubscribeForLifetime(nameof(Background), () => ProtectedPanel.Background = Background, this);
        this.SubscribeForLifetime(nameof(Foreground), () => ProtectedPanel.Foreground = Foreground, this);
    }

    protected override void OnPaint(ConsoleBitmap context) => Compose(ProtectedPanel);
}