namespace klooie.Gaming;

public class GameCollider : ConsolePanel
{   
    public Velocity Velocity { get; private set; }

    public virtual bool AutoAddToColliderGroup => true;

    public GameCollider(ColliderGroup group = null)
    {
        Velocity = new Velocity(this, group ?? Game.Current?.MainColliderGroup);
        Velocity.OnAngleChanged.Subscribe(() => FirePropertyChanged(nameof(Bounds)), this);
    }

    public GameCollider(RectF bounds, ColliderGroup group = null) : this(group)
    {
        this.Bounds = bounds;
    }

    public GameCollider(float x, float y, float w, float h, ColliderGroup group = null) : this(new RectF(x, y, w, h), group) { } 

    public GameCollider GetObstacleIfMovedTo(RectF area) =>  
            Velocity.GetObstacles()
            .Where(c => c.Bounds.Touches(area))
            .WhereAs<GameCollider>()
            .FirstOrDefault();

    public virtual bool CanCollideWith(GameCollider other) => object.ReferenceEquals(this, other) == false && other.Velocity.Group == this.Velocity.Group;


    public IEnumerable<GameCollider> GetObstacles() => Velocity.Group.GetObstacles(this).WhereAs<GameCollider>();
}


public class ColliderBox : GameCollider
{
    public override bool AutoAddToColliderGroup => false;
    public ColliderBox(RectF bounds) : base(bounds) { }
    public ColliderBox(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { }
}

