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
        DelayState dependencyTracker = DelayState.Create(this);
        if(parent != null) dependencyTracker.AddDependency(parent);
        if (cancellationLifetime != null) dependencyTracker.AddDependency(cancellationLifetime);
        dependencyTracker.AddDependency(Options.Vision);
        dependencyTracker.AddDependency(Options.Velocity);
        dependencyTracker.AddDependency(Options.Velocity.Collider);

        try
        {
            await Move();
            if (dependencyTracker.AreAllDependenciesValid == false) return false;
            return true;
        }
        finally
        {
            TryDispose();
            dependencyTracker.Dispose();
        }
    }
}
