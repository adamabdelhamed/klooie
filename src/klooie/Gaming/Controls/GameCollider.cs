namespace klooie.Gaming;

public class GameCollider : ConsoleControl
{   
    public Velocity Velocity { get; private set; }
    internal virtual bool AutoAddToColliderGroup => true;
    public virtual bool CanMoveTo(RectF bounds) => true;
    public GameCollider(ColliderGroup? group = null) => Velocity = new Velocity(this, group ?? Game.Current?.MainColliderGroup ?? (AutoAddToColliderGroup == false ? null : throw new ArgumentException($"{nameof(group)} can only be null when Game.Current is not")));
    public GameCollider(RectF bounds, ColliderGroup? group = null) : this(group) => this.Bounds = bounds;
    public GameCollider(float x, float y, float w, float h, ColliderGroup? group = null) : this(new RectF(x, y, w, h), group) { }
    public virtual bool CanCollideWith(GameCollider other) => this.IsVisible && ReferenceEquals(this, other) == false && other.Velocity.Group == this.Velocity.Group;
    public IEnumerable<GameCollider> GetObstacles() => Velocity.Group.GetObstacles(this);

    public bool TryMoveBy(float x, float y) => TryMoveTo(Left + x, Top + y);

    public bool TryMoveByRadial(Angle a, float distance)
    {
        var spot = Bounds.RadialOffset(a, distance);
        return TryMoveTo(spot.Left, spot.Top);
    }

    public void MoveByRadial(Angle a, float distance)
    {
        var spot = Bounds.RadialOffset(a, distance);
        MoveTo(spot.Left, spot.Top);
    }

    public bool TryMoveTo(float x, float y)
    {
        var proposedBounds = new RectF(x, y, Bounds.Width, Bounds.Height);
        if (CanMoveTo(proposedBounds) == false) return false;

        bool causesOverlap = false;
#if DEBUG
        var overlaps = GetObstacles().Where(o => o.CalculateDistanceTo(proposedBounds) == 0).ToArray();
        causesOverlap = overlaps.Any();
#else
        causesOverlap = GetObstacles().Any(o => o.CalculateDistanceTo(proposedBounds) == 0);
#endif

        if (causesOverlap == false)
        {
            this.MoveTo(x, y);
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool IsOverlappingAnyObstacles()
    {
        bool isOverlapped = false;
#if DEBUG
        var overlaps = GetOverlappingObstacles().ToArray();
        isOverlapped = overlaps.Any();
        if(isOverlapped)
        {
            // place for a breakpoint
        }
#else
        isOverlapped = GetOverlappingObstacles().Any();
#endif
        return isOverlapped;
    }

    public IEnumerable<GameCollider> GetOverlappingObstacles() => GetObstacles().Where(o => o.NumberOfPixelsThatOverlap(Bounds) > 0);
}

public sealed class ColliderBox : GameCollider
{
    internal override bool AutoAddToColliderGroup => false;
    public ColliderBox(RectF bounds) : base(bounds) { }
    public ColliderBox(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { }
}
