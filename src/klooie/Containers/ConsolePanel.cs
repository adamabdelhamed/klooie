namespace klooie;

/// <summary>
/// A console control that has nested control within its bounds
/// </summary>
public class ConsolePanel : Container
{
    private static readonly Comparison<ConsoleControl> CompareZ = new Comparison<ConsoleControl>((a, b) =>
        a.ZIndex == b.ZIndex ? a.ParentIndex.CompareTo(b.ParentIndex) : a.ZIndex.CompareTo(b.ZIndex));

    private List<ConsoleControl> sortedControls = new List<ConsoleControl>();

    /// <summary>
    /// Gets the nested controls
    /// </summary>
    public ObservableCollection<ConsoleControl> Controls { get; private set; }

    /// <summary>
    /// All nested controls, including those that are recursively nested within inner console panels
    /// </summary>
    public override IEnumerable<ConsoleControl> Children => Controls;

    /// <summary>
    /// Creates a new console panel
    /// </summary>
    public ConsolePanel()
    {
        Controls = new ObservableCollection<ConsoleControl>();
        Controls.Added.Subscribe((c) =>
        {
            c.Parent = this;
            sortedControls.Add(c);
            SortZ();
            c.Subscribe(nameof(c.ZIndex), () => SortZ(), Controls.GetMembershipLifetime(c));
        }, this);
        Controls.AssignedToIndex.Subscribe((assignment) => throw new NotSupportedException("Index assignment is not supported in Controls collection"), this);
        Controls.Removed.Subscribe((c) =>
        {
            sortedControls.Remove(c);
            c.Parent = null;
        }, this);

        this.OnDisposed(() =>
        {
            foreach (var child in Controls.ToArray())
            {
                child.TryDispose();
            }
        });

        this.CanFocus = false;
    }

    /// <summary>
    /// Adds a control to the panel
    /// </summary>
    /// <typeparam name="T">the type of controls being added</typeparam>
    /// <param name="c">the control to add</param>
    /// <returns>the control that was added</returns>
    public T Add<T>(T c) where T : ConsoleControl
    {
        Controls.Add(c);
        return c;
    }

    /// <summary>
    /// Adds a collection of controls to the panel
    /// </summary>
    /// <param name="controls">the controls to add</param>
    public void AddRange(IEnumerable<ConsoleControl> controls) => controls.ForEach(c => Controls.Add(c));

    /// <summary>
    /// Paints this control
    /// </summary>
    /// <param name="context">the drawing surface</param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        foreach (var control in sortedControls)
        {
            if (control.Width > 0 && control.Height > 0 && control.IsVisible && IsInView(control))
            {
                Compose(control);
            }
        }

        foreach (var filter in Filters)
        {
            filter.Control = this;
            filter.Filter(Bitmap);
        }
    }

    private void SortZ()
    {
        for (var i = 0; i < sortedControls.Count; i++)
        {
            sortedControls[i].ParentIndex = i;
        }
        sortedControls.Sort(CompareZ);
        ConsoleApp.Current?.RequestPaint();
    }
}