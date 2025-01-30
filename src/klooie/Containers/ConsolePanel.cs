using System.Buffers;

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
    public override IReadOnlyList<ConsoleControl> Children => Controls;


    protected override void OnInit()
    {
        base.OnInit();
        Controls = ObservableCollectionPool<ConsoleControl>.Instance.Rent();
        Controls.Added.Subscribe(OnControlAddedInternal, this);
        Controls.Removed.Subscribe(OnControlRemovedInternal, this);
        this.OnDisposed(DisposeChildren);
        this.CanFocus = false;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Controls.Dispose();
        Controls = null;
    }

    private void DisposeChildren()
    {
        var count = Controls.Count;
        var buffer = ArrayPool<ConsoleControl>.Shared.Rent(count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                buffer[i] = Controls[i];
            }
            for (var i = 0; i < count; i++)
            {
                buffer[i].TryDispose();
            }
        }
        finally
        {
            ArrayPool<ConsoleControl>.Shared.Return(buffer);
        }
    }

    private void OnControlRemovedInternal(ConsoleControl c)
    {
        NotifyDescendentRemoved(c);
        sortedControls.Remove(c);
        c.Parent = null;
        c.TryDispose();
    }
    private Action _sortZDelegate;
    private void OnControlAddedInternal(ConsoleControl controlAddedDirectlyToThisConsolePanel)
    {
        if(controlAddedDirectlyToThisConsolePanel.Parent != null || controlAddedDirectlyToThisConsolePanel.HasBeenAddedToVisualTree)
        {
            throw new InvalidOperationException("The control has already been added to a visual tree");
        }
        controlAddedDirectlyToThisConsolePanel.Parent = this;
        sortedControls.Add(controlAddedDirectlyToThisConsolePanel);
        SortZ();
        _sortZDelegate = _sortZDelegate ?? SortZ;
        controlAddedDirectlyToThisConsolePanel.ZIndexChanged.Subscribe(_sortZDelegate, Controls.GetMembershipLifetime(controlAddedDirectlyToThisConsolePanel));

        NotifyDescendentsAdded(controlAddedDirectlyToThisConsolePanel);
    }

    private void NotifyDescendentsAdded(ConsoleControl controlAddedDirectlyToThisConsolePanel)
    {
        Container container = this;
        while (container != null)
        {
            NotifyDescendentsAddedRecursive(controlAddedDirectlyToThisConsolePanel, container);
            container = container.Parent;
        }
    }

    private void NotifyDescendentsAddedRecursive(ConsoleControl added, Container toNotify)
    {
        if (added is Container cAsContainer)
        {
            for (var i = 0; i < cAsContainer.Children.Count; i++)
            {
                NotifyDescendentsAddedRecursive(cAsContainer.Children[i], toNotify);
            }
        }

        if (added.HasBeenAddedToVisualTree == false)
        {
            toNotify.DescendentAdded.Fire(added);
        }
    }

    private void NotifyDescendentRemoved(ConsoleControl controlRemovedDirectlyFromThisConsolePanel)
    {
        Container container = this;
        while (container != null)
        {
            container.DescendentRemoved.Fire(controlRemovedDirectlyFromThisConsolePanel);
            if (controlRemovedDirectlyFromThisConsolePanel is Container cAsContainer)
            {
                var buffer = DescendentBufferPool.Rent();
                try
                {
                    cAsContainer.PopulateDescendentsWithZeroAllocations(buffer);
                    for (var i = 0; i < buffer.Count; i++)
                    {
                        var descendent = buffer[i];
                        container.DescendentRemoved.Fire(descendent);
                    }
                }
                finally
                {
                    DescendentBufferPool.Return(buffer);
                }
            }
            container = container.Parent;
        }
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
        for (int i = 0; i < sortedControls.Count; i++)
        {
            ConsoleControl? control = sortedControls[i];
            if (control.Width > 0 && control.Height > 0 && control.IsVisible && IsInView(control))
            {
                Compose(control);
            }
        }

        for (int i = 0; i < Filters.Count; i++)
        {
            IConsoleControlFilter? filter = Filters[i];
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