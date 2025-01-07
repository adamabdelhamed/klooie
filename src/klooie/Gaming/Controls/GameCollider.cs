namespace klooie.Gaming;

public class GameCollider : ConsoleControl
{
    private bool connectToMainColliderGroup;
    public Velocity Velocity { get; private set; }
    internal virtual bool AutoAddToColliderGroup => true;
    public virtual bool CanMoveTo(RectF bounds) => true;


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

    protected override void ProtectedInit()
    {
        base.ProtectedInit();

        if(Velocity == null && connectToMainColliderGroup)
        {
            ConnectToGroup(Game.Current?.MainColliderGroup);
        }

        this.OnDisposed(this, ReturnVelocity);
    }

    private static void ReturnVelocity(object colliderObj)
    {
        var c = (colliderObj as GameCollider);
        VelocityPool.Instance.Return(c.Velocity);
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
    public virtual bool CanCollideWith(GameCollider other) => this.IsVisible && ReferenceEquals(this, other) == false && other.Velocity.Group == this.Velocity.Group;
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
            ObstacleBufferPool.Instance.Return(buffer);
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
                if (other.CalculateDistanceTo(proposedBounds) == 0)
                {
                    causesOverlap = true;
                    break;
                }
            }
        }
        finally
        {
            ObstacleBufferPool.Instance.Return(buffer);
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
        ObstacleBufferPool.Instance.Return(buffer);
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

public sealed class ColliderBox : GameCollider
{
    internal override bool AutoAddToColliderGroup => false;
    public ColliderBox(RectF bounds, bool connectToMainColliderGroup = true) : base(bounds, connectToMainColliderGroup) { }
    public ColliderBox()
    {

    }
    public ColliderBox(float x, float y, float w, float h, bool connectToMainColliderGroup = true) : this(new RectF(x, y, w, h), connectToMainColliderGroup) { }
}

 
