namespace klooie;

public partial class Rectangular :  Recyclable, IObservableObject, ICollidable
{
    private int x, y, w, h;
 
    public virtual bool CanCollideWith(ICollidable other) => true;
    public partial int ZIndex { get; set; }

    internal int ColliderHashCode { get; set; } = -1;

    public partial RectF Bounds { get; set; }

    public int Width
    {
        get
        {
            return w;
        }
        set
        {
            if (w == value) return;
            w = value;
            SyncBounds(new RectF(Bounds.Left, Bounds.Top, w, Bounds.Height));
        }
    }
    public int Height
    {
        get
        {
            return h;
        }
        set
        {
            if (h == value) return;
            h = value;
            SyncBounds(new RectF(Bounds.Left, Bounds.Top, Bounds.Width, h));
        }
    }
    public int X
    {
        get
        {
            return x;
        }
        set
        {
            if (x == value) return;
            x = value;
            SyncBounds(new RectF(x, Bounds.Top, Bounds.Width, Bounds.Height));
        }
    }
    public int Y
    {
        get
        {
            return y;
        }
        set
        {
            if (y == value) return;
            y = value;
            SyncBounds(new RectF(Bounds.Left, y, Bounds.Width, Bounds.Height));
        }
    }

    public float Left => Bounds.Left;

    public float Top => Bounds.Top;

    public Rectangular()
    {
 
    }

    protected override void OnInit()
    {
        base.OnInit();
        ColliderHashCode = -1;
        BoundsChanged.Subscribe(this, SyncBoundsFromExistingBounds, this);
    }

    public void MoveTo(LocF loc, int? z = null) => MoveTo(loc.Left, loc.Top, z);

    public void MoveTo(float x, float y, int? z = null)
    {
        Bounds = new RectF(x, y, Bounds.Width, Bounds.Height);
        if (z.HasValue)
        {
            ZIndex = z.Value;
        }
    }

    public void MoveBy(float x, float y, int z = 0)
    {
        Bounds = new RectF(Bounds.Left + x, Bounds.Top + y, Bounds.Width, Bounds.Height);
        ZIndex = ZIndex + z;
    }

    public void ResizeTo(float w, float h)
    {
        Bounds = new RectF(Bounds.Left, Bounds.Top, w, h);
    }

    public void MoveCenterTo(LocF loc) => MoveCenterTo(loc.Left, loc.Top);
    public void MoveCenterTo(float x, float y)
    {
        var left = x - Width / 2f;
        var top = y - Height / 2f;
        MoveTo(left, top);
    }

    public void ResizeBy(float w, float h)
    {
        Bounds = new RectF(Bounds.Left, Bounds.Top, Width + w, Height + h);
    }

    /// <summary>
    /// Moves the object forward by 0.1, down by 0.1, thinner by 0.2 and shorter by 0.2.
    /// This is useful when you want an object to appear to take up an entire pixel but to
    /// leave a little wiggle room for things like collision detection.
    /// 
    /// </summary>
    public void GiveWiggleRoom()
    {
        MoveBy(.1f, .1f);
        ResizeTo(Width - .2f, Height - .2f);
    }

    private void SyncBounds(RectF newBounds)
    {
        var newX = ConsoleMath.Round(newBounds.Left);
        var newY = ConsoleMath.Round(newBounds.Top);
        var newW = ConsoleMath.Round(newBounds.Width);
        var newH = ConsoleMath.Round(newBounds.Height);
        x = newX;
        y = newY;
        w = newW;
        h = newH;
        Bounds = newBounds;
    }

    private static void SyncBoundsFromExistingBounds(object me)
    {
        var self = (Rectangular)me;
        var newBounds = self.Bounds;
        var newX = ConsoleMath.Round(newBounds.Left);
        var newY = ConsoleMath.Round(newBounds.Top);
        var newW = ConsoleMath.Round(newBounds.Width);
        var newH = ConsoleMath.Round(newBounds.Height);
        self.x = newX;
        self.y = newY;
        self.w = newW;
        self.h = newH;
    }

    public float NumberOfPixelsThatOverlap(RectF other) => this.Bounds.NumberOfPixelsThatOverlap(other);
    public float NumberOfPixelsThatOverlap(Rectangular other) => this.Bounds.NumberOfPixelsThatOverlap(other.Bounds);

    public float OverlapPercentage(RectF other) => this.Bounds.OverlapPercentage(other);
    public float OverlapPercentage(Rectangular other) => this.Bounds.OverlapPercentage(other.Bounds);

    public bool Touches(RectF other) => this.Bounds.Touches(other);
    public bool Touches(Rectangular other) => this.Bounds.Touches(other.Bounds);

    public bool Contains(RectF other) => this.Bounds.Contains(other);
    public bool Contains(Rectangular other) => this.Bounds.Contains(other.Bounds);

    public float Bottom() => this.Bounds.Bottom;
    public float Right() => this.Bounds.Right;


    public LocF TopRight() => this.Bounds.TopRight;
    public LocF BottomRight() => this.Bounds.BottomRight;
    public LocF TopLeft() => this.Bounds.TopLeft;
    public LocF BottomLeft() => this.Bounds.BottomLeft;

    public LocF Center() => this.Bounds.Center;
    public float CenterX() => this.Bounds.CenterX;
    public float CenterY() => this.Bounds.CenterY;

    public RectF Round() => this.Bounds.Round();

    public RectF RadialOffset(Angle a, float d, bool normalized = true) => this.Bounds.RadialOffset(a, d, normalized);
    public RectF Offset(float dx, float dy) => this.Bounds.Offset(dx, dy);

    public Angle CalculateAngleTo(RectF other) => this.Bounds.CalculateAngleTo(other);
    public Angle CalculateAngleTo(Rectangular other) => this.Bounds.CalculateAngleTo(other.Bounds);

    public float CalculateDistanceTo(RectF other) => this.Bounds.CalculateDistanceTo(other);
    public float CalculateDistanceTo(Rectangular other) => this.Bounds.CalculateDistanceTo(other.Bounds);

    public float CalculateNormalizedDistanceTo(RectF other) => this.Bounds.CalculateNormalizedDistanceTo(other);
    public float CalculateNormalizedDistanceTo(Rectangular other) => this.Bounds.CalculateNormalizedDistanceTo(other.Bounds);

    public LocF[] Corners() => Bounds.Corners;

}

public static class RectangularEx
{
    public static float CalculateDistanceTo(this RectF rect, ConsoleControl collider) =>
        rect.CalculateDistanceTo(collider.Left, collider.Top, collider.Bounds.Width, collider.Bounds.Height);

    public static Angle CalculateAngleTo(this RectF rect, ConsoleControl collider) =>
        rect.CalculateAngleTo(collider.Left, collider.Top, collider.Bounds.Width, collider.Bounds.Height);
}

public interface ICollidable
{
    RectF Bounds { get; }
    bool CanCollideWith(ICollidable other);
}