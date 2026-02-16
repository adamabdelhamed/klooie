using System.Runtime.CompilerServices;

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
    /// <summary>
    /// Use the top layer's character and its foreground color as long as it is not a space.
    /// Otherwise use the bottom layer's character entirely. The bottom layer's background color
    /// is always used.
    /// </summary>
    BlendForeground = 3,
    /// <summary>
    /// Treat pixels that are a space ' ' AND have the default background color as transparent
    /// (i.e., do not draw them; let the bottom pixel show through). All other pixels paint over.
    /// </summary>
    TransparentSpaceDefaultBG = 4,
}
public abstract class Container : ConsoleControl
{
    public static ICompositionObserver? CompositionObserver { get; set; }

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
        if (clear) buffer.Clear();
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
                filter.ParentBitmap = this.Bitmap;
                filter.Filter(control.Bitmap);
                filter.ParentBitmap = null;
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
        else if (control.CompositionMode == CompositionMode.BlendVisible)
        {
            ComposeBlendVisible(control);
        }
        else if (control.CompositionMode == CompositionMode.BlendForeground)
        {
            ComposeBlendForeground(control);
        }
        else if (control.CompositionMode == CompositionMode.TransparentSpaceDefaultBG)
        {
            ComposeTransparentSpaceDefaultBG(control);
        }
        else
        {
            ComposeBlendVisible(control);
        }

    }

    public virtual (int X, int Y) Transform(ConsoleControl c) => (c.X, c.Y);

    private void ComposePaintOver(ConsoleControl control)
    {
        var obs = CompositionObserver;
        var ownerId = obs == null ? 0 : obs.GetId(control);
        var position = Transform(control);

        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);

        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                Bitmap.SetPixel(x, y, control.Bitmap.GetPixel(x - position.X, y - position.Y));
                obs?.OnPixelWritten(x, y, ownerId);
            }
        }
    }

    private void ComposeBlendBackground(ConsoleControl control)
    {
        var obs = CompositionObserver;
        var ownerId = obs == null ? 0 : obs.GetId(control);
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
                var myPixel = Bitmap.GetPixel(x, y);
                var controlIsDefaultBg = controlPixel.BackgroundColor == ConsoleString.DefaultBackgroundColor;
                var blend = controlIsDefaultBg;
                Bitmap.SetPixel(x, y, blend ? new ConsoleCharacter(controlPixel.Value, controlPixel.ForegroundColor, myPixel.BackgroundColor) : controlPixel);
                obs?.OnPixelWritten(x, y, ownerId);
            }
        }
    }

    private void ComposeBlendVisible(ConsoleControl control)
    {
        var obs = CompositionObserver;
        var ownerId = obs == null ? 0 : obs.GetId(control);
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
                Bitmap.SetPixel(x, y, vis ? controlPixel : Bitmap.GetPixel(x, y));
                if(vis) obs?.OnPixelWritten(x, y, ownerId);
            }
        }
    }

    private void ComposeBlendForeground(ConsoleControl control)
    {
        var obs = CompositionObserver;
        var ownerId = obs == null ? 0 : obs.GetId(control);
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var topPixel = control.Bitmap.GetPixel(x - position.X, y - position.Y);
                var bottomPixel = Bitmap.GetPixel(x, y);

                if (topPixel.Value != ' ')
                {
                    // Use top char and its FG, but always bottom BG
                    Bitmap.SetPixel(x, y, new ConsoleCharacter(topPixel.Value, topPixel.ForegroundColor, bottomPixel.BackgroundColor));
                    obs?.OnPixelWritten(x, y, ownerId);
                }
                else
                {
                    // Top is space: keep bottom entirely
                    Bitmap.SetPixel(x, y, bottomPixel);
                }
            }
        }
    }

    private void ComposeTransparentSpaceDefaultBG(ConsoleControl control)
    {
        var obs = CompositionObserver;
        var ownerId = obs == null ? 0 : obs.GetId(control);
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);

        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var top = control.Bitmap.GetPixel(x - position.X, y - position.Y);

                // If top is a space AND uses the default background color, skip drawing (transparent)
                if (top.Value == ' ' && top.BackgroundColor == ConsoleString.DefaultBackgroundColor)
                {
                    // no-op: leave bottom pixel as-is
                    continue;
                }

                // Otherwise, paint over like the default PaintOver mode
                Bitmap.SetPixel(x, y, top);
                obs?.OnPixelWritten(x, y, ownerId);
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

public interface ICompositionObserver
{
    int GetId(ConsoleControl control);

    // called only when the parent bitmap pixel is actually changed by the composition
    void OnPixelWritten(int x, int y, int ownerId);
}

public sealed class CompositionOwnerCapture : ICompositionObserver
{
    private int[] ownerIds = Array.Empty<int>();
    private int width;
    private int height;

    public Func<ConsoleControl, int> IdProvider { get; set; }

    public void Begin(int w, int h)
    {
        width = w;
        height = h;
        var needed = w * h;
        if (ownerIds.Length != needed) ownerIds = new int[needed];
        Array.Fill(ownerIds, 0);
    }

    public int[] SnapshotOwners()
    {
        var copy = new int[ownerIds.Length];
        Array.Copy(ownerIds, copy, ownerIds.Length);
        return copy;
    }

    public int GetId(ConsoleControl control) => IdProvider(control);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnPixelWritten(int x, int y, int ownerId)
    {
        var index = (y * width) + x;
        if (index < 0 || index >= ownerIds.Length) return;
        ownerIds[index] = ownerId;
    }
}