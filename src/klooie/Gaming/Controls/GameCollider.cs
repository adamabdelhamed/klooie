namespace klooie.Gaming;

public class GameCollider : ConsoleControl
{
    internal float lastEvalTime;
    public float MinNextEvalTime => this.lastEvalTime + EvalFrequencySeconds;
    public float EvalFrequencySeconds => (Velocity.Speed > ColliderGroup.HighestSpeedForEvalCalc ? ColliderGroup.MostFrequentEval : ColliderGroup.EvalFrequencySlope * Velocity.speed + ColliderGroup.LeastFrequentEval);

    private bool connectToMainColliderGroup; // todo - remove this since I don't think there's any path where it can actually be set in time for OnInit
    public Velocity Velocity { get; private set; }
    public ColliderGroup ColliderGroup { get; private set; }
    internal virtual bool AutoAddToColliderGroup => true;
    public virtual bool CanMoveTo(RectF bounds) => true;

    private float lastSpeed;

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
    }
 
    public void ConnectToGroup(ColliderGroup group)
    {
        if(group == null) throw new ArgumentNullException(nameof(group));
        if (ColliderGroup != null) throw new ArgumentException("This collider is already connected to a group");
        Velocity = VelocityPool.Instance.Rent();
        Velocity.OnSpeedChanged.Subscribe(this, UpdateLastEvalTime, this);
        this.ColliderGroup = group;
        group.Register(this);
    }

    private static void UpdateLastEvalTime(GameCollider collider)
    {
        if (collider.lastSpeed == 0 && collider.Velocity.Speed > 0)
        {
            collider.lastEvalTime = (float)collider.ColliderGroup.Now.TotalSeconds;
        }
        collider.lastSpeed = collider.Velocity.Speed;
    }

    public GameCollider(RectF bounds, bool connectToMainColliderGroup = true) : this(connectToMainColliderGroup) => this.Bounds = bounds;
    public GameCollider(float x, float y, float w, float h, bool connectToMainColliderGroup = true) : this(new RectF(x, y, w, h), connectToMainColliderGroup) { }
    public override bool CanCollideWith(ICollidable other)
    {
        if(base.CanCollideWith(other) == false) return false;
        if(IsVisible == false || ReferenceEquals(this, other)) return false;

        return true;
    }
    public void GetObstacles(ObstacleBuffer buffer) => ColliderGroup.GetObstacles(this, buffer);

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

    protected override void OnReturn()
    {
        base.OnReturn();
        var temp = Velocity;
        Velocity?.TryDispose("By owning collider");
        Velocity = null;
        ColliderGroup = null;
        UniformGrid?.Remove(this);
        UniformGrid = null;
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

 
