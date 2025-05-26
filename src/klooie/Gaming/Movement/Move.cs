namespace klooie.Gaming;


public class ShortCircuitException : Exception { }
public class Mover
{
    public const float DefaultCloseEnough = .8f;

    public static async Task Invoke(Movement parent, Movement process) => await process.InvokeInternal(parent);
    public static async Task<T> InvokeGet<T>(Movement parent, Movement<T> process) { await Invoke(parent, process); return process.Result; }
    public static async Task<T> InvokeGetWithShortCircuit<T>(Movement<T> process) { await InvokeWithShortCircuit(process); return process.Result; }
    public static async Task<T> InvokeGetOrTimeout<T>(Movement parent, Movement<T> process, ILifetime timeout) { await InvokeOrTimeout(parent, process, timeout); return process.Result; }
    public static async Task<T> InvokeGetOrTimeout<T>(Movement parent, Movement<T> process, float timeout) { await InvokeOrTimeout(parent, process, timeout); return process.Result; }

    public static async Task InvokeWithShortCircuit(Movement process)
    {
        try
        {
            await process.InvokeInternal(null);
        }
        catch (ShortCircuitException)
        {
            // The velocity expired
        }
    }

    public static Task<bool> InvokeOrTimeout(Movement parent, Movement process, float timeout)
    {
        return InvokeOrTimeout(parent, process, Recyclable.EarliestOf(parent, process, Game.Current.Delay(timeout).ToLifetime()));
    }

    public static async Task<bool> InvokeOrTimeout(Movement parent, Movement process, ILifetime maxLifetime)
    {
        var lease = maxLifetime.Lease;
        try
        {
            maxLifetime.OnDisposed(() => process.TryDispose());
            await process.InvokeInternal(parent);
            return true;
        }
        catch (ShortCircuitException)
        {
            if (maxLifetime.IsStillValid(lease) == false)
            {
                return false;
            }
            else
            {
                throw;
            }
        }
    }

    public static async Task<bool> InvokeOrTimeout(Movement process, ILifetime maxLifetime)
    {
        try
        {
            maxLifetime.OnDisposed(() => process.TryDispose());
            await process.InvokeInternal(null);
            return true;
        }
        catch (ShortCircuitException)
        {
            return false;
        }
    }
}


public delegate float SpeedEval();

public abstract class Movement<T> : Movement
{
    protected Movement() : base() { }
    public abstract T Result { get; protected set; }
}

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
    public GameCollider Element => Options.Velocity.Collider as GameCollider;

    private Movement parent;

    protected Movement()
    {

    }

    protected void Bind(MovementOptions options)
    {
        Options = options;
        Options.Vision.OnDisposed(this, DisposeMeStatic);
        Options.Velocity.OnDisposed(this, DisposeMeStatic);
    }

    private static void DisposeMeStatic(object movement)
    {
        ((Recyclable)movement).TryDispose();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
    }



 
    protected abstract Task Move();

    internal async Task InvokeInternal(Movement parent)
    {
        var lease = Lease;
        var parentLease = parent?.Lease;
        try
        {
            this.parent = parent;
            AssertAlive(lease, parentLease);
            await Move();
            AssertAlive(lease, parentLease);
        }
        finally
        {
            TryDispose();
        }
    }

    public void AssertAlive(int lease, int? parentLease = null)
    {
        if (IsStillValid(lease) == false)
        {
            throw new ShortCircuitException();
        }

        if (parent != null && parentLease.HasValue)
        {
            parent.AssertAlive(parentLease.Value);
        }
    }

  


    protected async Task Delay(double ms)
    {
        await Game.Current.Delay(ms);
    }

    protected async Task Delay(TimeSpan timeout)
    {
        await Game.Current.Delay(timeout);
    }

    protected async Task DelayFuzzyAsync(float ms, float maxDeltaPercentage = 0.1f)
    {
        await Game.Current.DelayFuzzy(ms, maxDeltaPercentage);
    }

}
