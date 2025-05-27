namespace klooie.Gaming;

public class GameCollider : ConsoleControl
{
    private bool connectToMainColliderGroup;
    public Velocity Velocity { get; private set; }
    internal virtual bool AutoAddToColliderGroup => true;
    public virtual bool CanMoveTo(RectF bounds) => true;

    // Only used by UniformGrid to avoid allocations in a callback. The grid
    // passes a collider as the object to the callback, so we can use this
    // to lookup the grid that the collider is in. This allows the callback
    // to be static and avoid allocations.
    internal UniformGrid? UniformGrid { get; set; }

    public GameCollider() : this(true)
    {
    
    }

    public GameCollider(bool connectToMainColliderGroup)
    {
        this.connectToMainColliderGroup = connectToMainColliderGroup;
        if (connectToMainColliderGroup)
        {
            ConnectToGroup(Game.Current?.MainColliderGroup);
        }
    }

    protected override void OnInit()
    {
        base.OnInit();

        if(Velocity == null && connectToMainColliderGroup)
        {
            ConnectToGroup(Game.Current?.MainColliderGroup);
        }

        this.OnDisposed(this, ReturnVelocity);
    }

    private static void ReturnVelocity(object colliderObj)
    {
        var c = (colliderObj as GameCollider);
        c.Velocity.Dispose();
        c.Velocity = null;
    }

    public void ConnectToGroup(ColliderGroup group)
    {
        if(group == null) throw new ArgumentNullException(nameof(group));
        if (Velocity?.Group != null) throw new ArgumentException("This collider is already connected to a group");
        Velocity = VelocityPool.Instance.Rent();
        Velocity.Init(this, group);
    }

    public GameCollider(RectF bounds, bool connectToMainColliderGroup = true) : this(connectToMainColliderGroup) => this.Bounds = bounds;
    public GameCollider(float x, float y, float w, float h, bool connectToMainColliderGroup = true) : this(new RectF(x, y, w, h), connectToMainColliderGroup) { }
    public override bool CanCollideWith(ICollidable other)
    {
        if(base.CanCollideWith(other) == false) return false;
        if(IsVisible == false || ReferenceEquals(this, other)) return false;
        if(other is GameCollider otherCollider && otherCollider.Velocity?.Group != this.Velocity.Group) return false;

        return true;
    }
    public void GetObstacles(ObstacleBuffer buffer) => Velocity.Group.GetObstacles(this, buffer);

    public GameCollider[] GetObstacles()
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            GetObstacles(buffer);
            return buffer.ReadableBuffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

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
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            GetObstacles(buffer);
            for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
            {
                var other = buffer.WriteableBuffer[i];
                if (other.CalculateDistanceTo(proposedBounds) < CollisionDetector.VerySmallNumber)
                {
                    causesOverlap = true;
                    break;
                }
            }
        }
        finally
        {
            buffer.Dispose();
        }
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
        var buffer = ObstacleBufferPool.Instance.Rent();
        GetOverlappingObstacles(buffer);
        isOverlapped = buffer.WriteableBuffer.Count > 0;
        buffer.Dispose();
        return isOverlapped;
    }

    public void GetOverlappingObstacles(ObstacleBuffer buffer)
    {
        GetObstacles(buffer);
        for(var i = 0; i < buffer.WriteableBuffer.Count; i++)
        {
            var other = buffer.WriteableBuffer[i];
            if(other.NumberOfPixelsThatOverlap(Bounds) == 0)
            {
                buffer.WriteableBuffer.RemoveAt(i);
                i--;
            }

        }
    }
}

public sealed class ColliderBox : Recyclable, ICollidable
{
    public RectF Bounds { get; set; }  


    public ColliderBox(RectF bounds) => this.Bounds = bounds;
    public ColliderBox() { }
    public ColliderBox(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { }

    public bool CanCollideWith(ICollidable other) => true;
}

 
