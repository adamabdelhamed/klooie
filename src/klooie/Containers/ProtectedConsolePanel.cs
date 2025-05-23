﻿namespace klooie;
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
    /// Gets the children of this control
    /// </summary>
    public override IReadOnlyList<ConsoleControl> Children => [ProtectedPanel];

    protected override void OnInit()
    {
        base.OnInit();
        this.CanFocus = false;
        ProtectedPanel = new ConsolePanel();
        ProtectedPanel.Parent = this;
        ProtectedPanel.Fill();
        this.BackgroundChanged.Sync(() => ProtectedPanel.Background = Background, this);
        this.ForegroundChanged.Sync(() => ProtectedPanel.Foreground = Foreground, this);
        OnDisposed(() =>
        {
            ProtectedPanel?.TryDispose();
            ProtectedPanel = null;
        });
    }

    protected override void OnPaint(ConsoleBitmap context) => Compose(ProtectedPanel);
}