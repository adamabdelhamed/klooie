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
            .Where(c => c.MassBounds.Touches(area))
            .WhereAs<GameCollider>()
            .FirstOrDefault();

    public virtual bool CanCollideWith(GameCollider other) => object.ReferenceEquals(this, other) == false && other.Velocity.Group == this.Velocity.Group;

  



    public IEnumerable<GameCollider> GetObstacles() => Velocity.Group.GetObstacles(this).WhereAs<GameCollider>();


    public GameCollider GetRoot()
    {
        return (this as IAmMass)?.Parent ?? this;
    }

    public IEnumerable<GameCollider> GetAll()
    {
        var root = (this as ChildCharacter)?.ParentCollider ?? this;
        yield return root;
        if (this is ParentGameCollider)
        {
            foreach (var c in (this as ParentGameCollider).Children)
            {
                yield return c;
            }
        }
    }

    public virtual bool IsPartOfMass(GameCollider mass)
    {
        return mass == this || (mass as ParentGameCollider)?.Children.Contains(this) == true;
    }
}

public class ParentGameCollider : GameCollider
{
    public List<GameCollider> Children { get; private set; } = new List<GameCollider>();
    public bool SharedHPMode { get; protected set; }
    public override bool CanCollideWith(GameCollider other)
    {
        return base.CanCollideWith(other) && Children.Contains(other) == false;
    }

    public override RectF MassBounds
    {
        get
        {
            var left = Left;
            var top = Top;
            var right = this.Right();
            var bottom = this.Bottom();

            foreach (var child in Children)
            {
                left = Math.Min(left, child.Left);
                top = Math.Min(top, child.Top);
                right = Math.Max(right, child.Right());
                bottom = Math.Max(bottom, child.Bottom());
            }

            var bounds = new RectF(left, top, right - left, bottom - top);
            return bounds;
            
        }
    }

    public ParentGameCollider()
    {
        DamageDirective.Current?.OnDamageEnforced.Subscribe((args) =>
        {
            if (SharedHPMode == false) return;
            var damagee = args.RawArgs.Damagee as ChildCharacter;
            if (damagee == null) return;
            if (damagee.ParentCollider != this) return;

            DamageDirective.Current.ReportDamage(new DamageEventArgs()
            {
                Damagee = this,
                Damager = args.RawArgs.Damager
            });
        }, this);

        OnDisposed(Cleanup);
    }

    public void Cleanup()
    {
        foreach (var child in Children.ToArray())
        {
            child.Dispose();
        }
    }
}

public class ChildCharacter : Character
{
    private Character parent;

    public Character ParentCollider => parent;

    public ChildCharacter(Character parent)
    {
        this.parent = parent;
        parent.OnDisposed(() => this.TryDispose());
    }

    public override bool CanCollideWith(GameCollider other)
    {
        if (base.CanCollideWith(other) == false) return false;
        if (other == parent) return false;
        if ((other as ChildCharacter)?.Parent == Parent) return false;

        return true;
    }
}

public class ColliderBox : GameCollider
{
    public override bool AutoAddToColliderGroup => false;
    public ColliderBox(RectF bounds) : base(bounds) { }
    public ColliderBox(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { }
}

