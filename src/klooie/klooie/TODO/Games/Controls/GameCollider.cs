namespace klooie.Gaming;

public class GameCollider : ConsolePanel
{
    public ConsoleCharacter? Pen { get; set; }
    
    public Velocity Velocity { get; private set; }



    public GameCollider()
    {
        Velocity = new Velocity(this, Game.Current.MainColliderGroup);
        Velocity.OnAngleChanged.Subscribe(() => FirePropertyChanged(nameof(Bounds)), this);
    }

    public GameCollider(RectF bounds) : this()
    {
        this.Bounds = bounds;
    }

    public GameCollider(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { } 

    public GameCollider GetObstacleIfMovedTo(RectF area) =>  
            Velocity.GetObstaclesSlow()
            .Where(c => c.MassBounds.Touches(area))
            .WhereAs<GameCollider>()
            .FirstOrDefault();

    public virtual bool CanCollideWith(GameCollider other) => object.ReferenceEquals(this, other) == false && other.Velocity.Group == this.Velocity.Group;

  



    public IEnumerable<GameCollider> GetObstacles() => Velocity.Group.GetObstaclesSlow(this).WhereAs<GameCollider>();


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
    }

    public override bool CanCollideWith(GameCollider other)
    {
        if (base.CanCollideWith(other) == false) return false;
        if (other == parent) return false;
        if ((other as ChildCharacter)?.Parent == Parent) return false;

        return true;
    }
}

public static class GameColliderExtensions
{
    public static void MoveTo(this GameCollider c, float x, float y, int? z = null)
    {
        var e = c as ConsoleControl;
        if(z.HasValue)
        {
            e.ZIndex = z.Value;
        }

        c.Bounds = new RectF(x, y, e.Bounds.Width, e.Bounds.Height);
    }

    public static void MoveBy(this GameCollider c, float x, float y, int? z = null)
    {
        var e = c as ConsoleControl;
        if (z.HasValue)
        {
            e.ZIndex+= z.Value;
        }

        c.Bounds = new RectF(e.Bounds.Left + x, e.Bounds.Top + y, e.Bounds.Width, e.Bounds.Height);
    }

    public static IEnumerable<GameCollider> GetObstacles(this GameCollider c)
    {
        var e = c as GameCollider;
        return Game.Current.MainColliderGroup.GetObstaclesSlow(c);
    }
}



