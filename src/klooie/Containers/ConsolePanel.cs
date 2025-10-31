using System.Buffers;

namespace klooie;

/// <summary>
/// A console control that has nested control within its bounds
/// </summary>
public class ConsolePanel : Container
{
 

    private readonly List<ConsoleControl> sortedControls = new();
    private int _nextParentIndex; // stable, monotonic

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
        Controls.Added.Subscribe(OnControlAddedInternal, Controls);
        Controls.Removed.Subscribe(OnControlRemovedInternal, Controls);
        this.OnDisposed(Cleanup);
        this.CanFocus = false;
    }

    private void Cleanup()
    {
        var count = Controls.Count;
        var buffer = ArrayPool<ConsoleControl>.Shared.Rent(count);
        try
        {
            for (var i = 0; i < count; i++) buffer[i] = Controls[i];
            for (var i = 0; i < count; i++) buffer[i].TryDispose();
        }
        finally
        {
            ArrayPool<ConsoleControl>.Shared.Return(buffer);
        }
        Controls.Clear();
        Controls.Dispose();
        Controls = null;
        sortedControls.Clear();
    }

    private void OnControlRemovedInternal(ConsoleControl c)
    {
        NotifyDescendentRemoved(c);
        // Remove without re-sorting
        var idx = sortedControls.IndexOf(c);
        if (idx >= 0) sortedControls.RemoveAt(idx);
        c.Parent = null;
        c.TryDispose();
    }

    private void OnControlAddedInternal(ConsoleControl control)
    {
        if (!control.IsStillValid(control.Lease))
            throw new InvalidOperationException("The control being added is no longer valid.");

        if (control.Parent != null || control.HasBeenAddedToVisualTree)
            throw new InvalidOperationException("The control has already been added to a visual tree");

        control.Parent = this;

        // Assign a stable ParentIndex exactly once for this panel
        control.ParentIndex = ++_nextParentIndex;

        // Incremental insert keeps list sorted by (ZIndex, ParentIndex)
        InsertByZIndex(control);

        // Reinsert on Z changes
        control.ZIndexChanged.Subscribe(() =>
        {
            // Remove from current position (linear scan; Z changes are rare)
            var i = sortedControls.IndexOf(control);
            if (i >= 0) sortedControls.RemoveAt(i);
            InsertByZIndex(control);
        }, Controls.GetMembershipLifetime(control));

        NotifyDescendentsAdded(control);
    }

    private void InsertByZIndex(ConsoleControl c)
    {
        // Common case: same or higher Z than the tail → append O(1)
        if (sortedControls.Count == 0 ||
            c.ZIndex > sortedControls[^1].ZIndex ||
            (c.ZIndex == sortedControls[^1].ZIndex /* tie goes to newer due to larger ParentIndex */))
        {
            sortedControls.Add(c);
            return;
        }

        // Find first index with Z > c.ZIndex (upper bound for Z)
        int lo = 0;
        int hi = sortedControls.Count - 1;
        int insertAt = sortedControls.Count;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var z = sortedControls[mid].ZIndex;
            if (z <= c.ZIndex) lo = mid + 1;   // move right on equal; we want after equals
            else { insertAt = mid; hi = mid - 1; }
        }

        // insertAt now points to first element with Z > c.ZIndex (or Count if none)
        // For equal Z, we insert after the block of equals; ParentIndex already makes us last among equals.
        sortedControls.Insert(insertAt, c);
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
            var control = sortedControls[i];
            if (control.Width > 0 && control.Height > 0 && control.IsVisible && IsInView(control))
            {
                Compose(control);
            }
        }

        for (int i = 0; i < Filters.Count; i++)
        {
            var filter = Filters[i];
            filter.Control = this;
            filter.Filter(Bitmap);
        }
    }
}