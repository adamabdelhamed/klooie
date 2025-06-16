namespace klooie;

public enum CompositionMode
{
    /// <summary>
    /// The default. The control being painted always paints over whatever pixel is beneath it
    /// </summary>
    PaintOver = 0,
    /// <summary>
    /// If the control being painted's pixel has a default BG then use the background 
    /// color of the existing pixel instead of the control pixel. Otherwise behaves like PaintOver.
    /// </summary>
    BlendBackground = 1,
    /// <summary>
    /// If the control being painted's pixel would end up looking invisible on the parent panel
    /// then skip drawing it so that the pixel will end up looking transparent.
    /// </summary>
    BlendVisible = 2,
}
public abstract class Container : ConsoleControl
{
    private Event<ConsoleControl> _descendentAdded, _descendentRemoved;
    public Event<ConsoleControl> DescendentAdded { get => _descendentAdded ?? (_descendentAdded = Event<ConsoleControl>.Create()); }
    public Event<ConsoleControl> DescendentRemoved { get => _descendentRemoved ?? (_descendentRemoved = Event<ConsoleControl>.Create()); }
    public static SingleThreadObjectPool<List<ConsoleControl>> DescendentBufferPool { get; private set; } = new SingleThreadObjectPool<List<ConsoleControl>>();
    internal Container() 
    {
        OnDisposed(OnReturn);
    }

    /// <summary>
    /// Gets the children of this container
    /// </summary>
    public abstract IReadOnlyList<ConsoleControl> Children { get; }

    /// <summary>
    /// Gets all descendents of this container
    /// </summary>
    public IEnumerable<ConsoleControl> Descendents
    {
        get
        {
            var buffer = DescendentBufferPool.Rent();
            try
            {
                PopulateDescendentsWithZeroAllocations(buffer);
                return new List<ConsoleControl>(buffer);
            }
            finally
            {
                DescendentBufferPool.Return(buffer);
            }
        }
    }

    public void PopulateDescendentsWithZeroAllocations(List<ConsoleControl> buffer, bool clear = true)
    {
        if(clear) buffer.Clear();
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            buffer.Add(child);

            if (child is Container container)
            {
                container.PopulateDescendentsWithZeroAllocations(buffer, false);
            }
        }
    }

    /// <summary>
    /// Determines if the control is in view and should be painted
    /// </summary>
    /// <param name="c">the control</param>
    /// <returns>true if the control is in view and should be painted</returns>
    public virtual bool IsInView(ConsoleControl c) => new RectF(0, 0, Width, Height).Touches(c.Bounds);

    /// <summary>
    /// Determines if the rectangle is in view so that the caller can make decisions
    /// </summary>
    /// <param name="rect">the area to check</param>
    /// <returns>true if the area is in view</returns>
    public virtual bool IsInView(RectF rect) => new RectF(0, 0, Width, Height).Touches(rect);

    /// <summary>
    /// Visits every control in the control tree, recursively, using the visit action provided
    /// </summary>
    /// <param name="visitAction">the visitor function that will be run for each child control, the function can return true if it wants to stop further visitation</param>
    /// <param name="root">set to null, used for recursion</param>
    /// <returns>true if the visitation was short ciruited by a visitor, false otherwise</returns>
    public bool VisitControlTree(Func<ConsoleControl, bool> visitAction, Container root = null)
    {
        bool shortCircuit = false;
        root = root ?? this;

        for (int i = 0; i < root.Children.Count; i++)
        {
            ConsoleControl? child = root.Children[i];
            shortCircuit = visitAction(child);
            if (shortCircuit) return true;

            if (child is Container)
            {
                shortCircuit = VisitControlTree(visitAction, child as Container);
                if (shortCircuit) return true;
            }
        }

        return false;
    }

    protected void Compose(ConsoleControl control)
    {
        if (control.IsVisible == false) return;
        control.Paint();

        if (control._filters != null)
        {
            for (int i = 0; i < control.Filters.Count; i++)
            {
                var filter = control.Filters[i];
                filter.Control = control;
                filter.Filter(control.Bitmap);
            }
        }

        if (control.CompositionMode == CompositionMode.PaintOver)
        {
            ComposePaintOver(control);
        }
        else if (control.CompositionMode == CompositionMode.BlendBackground)
        {
            ComposeBlendBackground(control);
        }
        else
        {
            ComposeBlendVisible(control);
        }

    }

    public virtual (int X, int Y) Transform(ConsoleControl c) => (c.X, c.Y);

    private void ComposePaintOver(ConsoleControl control)
    {
        var position = Transform(control);

        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);

        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                Bitmap.SetPixel(x,y, control.Bitmap.GetPixel(x - position.X, y - position.Y));
            }
        }
    }

    private void ComposeBlendBackground(ConsoleControl control)
    {
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var controlPixel = control.Bitmap.GetPixel(x - position.X,y - position.Y);
                var myPixel = Bitmap.GetPixel(x, y);
                var controlIsDefaultBg = controlPixel.BackgroundColor == ConsoleString.DefaultBackgroundColor;
                var blend = controlIsDefaultBg;
                Bitmap.SetPixel(x,y, blend ? new ConsoleCharacter(controlPixel.Value, controlPixel.ForegroundColor, myPixel.BackgroundColor) : controlPixel);
            }
        }
    }

    private void ComposeBlendVisible(ConsoleControl control)
    {
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var controlPixel = control.Bitmap.GetPixel(x - position.X, y - position.Y);
                var vis = controlPixel.Value == ' ' ? controlPixel.BackgroundColor != Background : controlPixel.ForegroundColor != Background || controlPixel.BackgroundColor != Background;
                Bitmap.SetPixel(x,y, vis ? controlPixel : Bitmap.GetPixel(x,y));
            }
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _descendentAdded?.TryDispose();
        _descendentAdded = null;
        _descendentRemoved?.TryDispose();
        _descendentRemoved = null;
    }
}
