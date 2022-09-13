namespace klooie.Gaming;

public class GameCollider : ConsoleControl
{   
    public Velocity Velocity { get; private set; }
    internal virtual bool AutoAddToColliderGroup => true;
    public virtual bool CanMoveTo(RectF bounds) => true;
    public GameCollider(ColliderGroup? group = null) => Velocity = new Velocity(this, group ?? Game.Current?.MainColliderGroup ?? (AutoAddToColliderGroup == false ? null : throw new ArgumentException($"{nameof(group)} can only be null when Game.Current is not")));
    public GameCollider(RectF bounds, ColliderGroup? group = null) : this(group) => this.Bounds = bounds;
    public GameCollider(float x, float y, float w, float h, ColliderGroup? group = null) : this(new RectF(x, y, w, h), group) { } 
    public virtual bool CanCollideWith(GameCollider other) => ReferenceEquals(this, other) == false && other.Velocity.Group == this.Velocity.Group;
    public IEnumerable<GameCollider> GetObstacles() => Velocity.Group.GetObstacles(this).WhereAs<GameCollider>();
}

public sealed class ColliderBox : GameCollider
{
    internal override bool AutoAddToColliderGroup => false;
    public ColliderBox(RectF bounds) : base(bounds) { }
    public ColliderBox(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { }
}
