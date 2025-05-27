namespace klooie.Gaming;

public class Mover
{
    public const float DefaultCloseEnough = .8f;

    public static async Task<bool> Invoke(Movement parent, Movement process) => await process.InvokeInternal(parent);

    public static async Task<bool> Invoke(Movement process)
    {
        return await process.InvokeInternal(null);
    }

    public static async Task<bool> InvokeOrTimeout(Movement parent, Movement process, ILifetime maxLifetime)
    {
        maxLifetime.OnDisposed(process, DisposeLifetime);
        var result = await process.InvokeInternal(parent, maxLifetime);
        return result;
    }

    public static async Task<bool> InvokeOrTimeout(Movement process, ILifetime maxLifetime)
    {
        maxLifetime.OnDisposed(process, DisposeLifetime);
        var result = await process.InvokeInternal(null, maxLifetime);   
        return result;
    }

    private static void DisposeLifetime(object lifetime) => ((Recyclable)lifetime).TryDispose();
}

public delegate float SpeedEval();

public class MovementOptions
{
    public required Velocity Velocity { get; set; }
    public required SpeedEval Speed { get; set; }
    public required Vision Vision { get; set; }
}

public abstract class Movement : Recyclable
{
    protected float NowDisplay => ConsoleMath.Round(Options.Velocity.Group.Now.TotalSeconds, 2);
    public MovementOptions Options { get; private set; }
    public GameCollider Element => Options.Velocity.Collider;

    private Movement parent;

    protected Movement() { }
    protected void Bind(MovementOptions options)
    {
        Options = options;
    }

    protected abstract Task Move();

    /// <summary>
    /// Returns false if short-circuited; true if completed normally.
    /// </summary>
    internal async Task<bool> InvokeInternal(Movement parent, ILifetime cancellationLifetime = null)
    {
        var lease = Lease;
        var parentLease = parent?.Lease;
        var cancellationLease = cancellationLifetime?.Lease;
        var visionLease = Options.Vision?.Lease;
        var velocityLease = Options.Velocity?.Lease;
        var colliderLease = Options.Velocity?.Collider?.Lease;
        this.parent = parent;

        bool IsAlive()
        {
            bool selfValid = IsStillValid(lease);
            bool parentValid = parent == null || (parentLease.HasValue && parent.IsStillValid(parentLease.Value));
            bool lifetimeValid = cancellationLifetime == null || cancellationLifetime.IsStillValid(cancellationLease.Value);
            bool visionValid = Options.Vision == null || (visionLease.HasValue && Options.Vision.IsStillValid(visionLease.Value));
            bool velocityValid = Options.Velocity == null || (velocityLease.HasValue && Options.Velocity.IsStillValid(velocityLease.Value));
            bool colliderValid = Options.Velocity?.Collider == null || (colliderLease.HasValue && Options.Velocity.Collider.IsStillValid(colliderLease.Value));
            return selfValid && parentValid && lifetimeValid && visionValid && velocityValid && colliderValid;
        }

        try
        {
            if (!IsAlive()) return false;
            await Move();
            if (!IsAlive()) return false;
            return true;
        }
        finally
        {
            TryDispose();
        }
    }
}
