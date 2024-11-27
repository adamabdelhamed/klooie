namespace klooie;
/// <summary>
/// Represents the orientation of a 2d visual
/// </summary>
public enum Orientation
{
    /// <summary>
    /// Vertical orientation (up and down)
    /// </summary>
    Vertical,
    /// <summary>
    /// Horizontal orientation (left and right)
    /// </summary>
    Horizontal,
}

/// <summary>
/// A panel that handles stacking child controls
/// </summary>
public partial class StackPanel : ConsolePanel
{
    /// <summary>
    /// Specifies how auto sizing works
    /// </summary>
    public enum AutoSizeMode
    {
        // don't auto size
        None,
        // auto size only the height
        Height,
        // auto size only the width
        Width,
        // auto size both
        Both
    }

    /// <summary>
    /// Gets or sets the orientation of the control
    /// </summary>
    public partial Orientation Orientation { get; set; }

    /// <summary>
    /// Gets or sets the value, in number of console pixels to space between child elements.  Defaults to 0.
    /// </summary>
    public partial int Margin { get; set; }

    /// <summary>
    /// Control the auto size behavior of the stack panel Defaults to None.
    /// </summary>
    public AutoSizeMode AutoSize { get; set; }

    /// <summary>
    /// Creates a new stack panel
    /// </summary>
    public StackPanel()
    {
        BoundsChanged.Subscribe(RedoLayout, this);
        MarginChanged.Subscribe(RedoLayout, this);
        Controls.Added.Subscribe(Controls_Added, this);
        Controls.Changed.Subscribe(RedoLayout, this);
    }

    private void Controls_Added(ConsoleControl obj) => obj.BoundsChanged.Sync(RedoLayout, Controls.GetMembershipLifetime(obj));
    
    private void RedoLayout()
    {
        if (this.IsExpired || this.IsExpiring) return;
        if (Orientation == Orientation.Vertical)
        {
            int h = Layout.StackVertically(Margin, Controls);
            if (AutoSize == AutoSizeMode.Height || AutoSize == AutoSizeMode.Both)
            {
                Height = h;
            }

            if (AutoSize == AutoSizeMode.Width || AutoSize == AutoSizeMode.Both)
            {
                Width = Controls.Count == 0 ? 0 : Controls.Select(c => c.X + c.Width).Max();
            }
        }
        else
        {
            int w = Layout.StackHorizontally(Margin, Controls);
            if (AutoSize == AutoSizeMode.Width || AutoSize == AutoSizeMode.Both)
            {
                Width = w;
            }

            if (AutoSize == AutoSizeMode.Height || AutoSize == AutoSizeMode.Both)
            {
                Height = Controls.Count == 0 ? 0 : Controls.Select(c => c.Y + c.Height).Max();
            }
        }
    }
}
