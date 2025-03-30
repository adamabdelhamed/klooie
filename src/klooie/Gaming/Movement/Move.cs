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

    protected virtual void Bind(Velocity v, SpeedEval speed)
    {
        base.Bind(v, speed);
    }

    public abstract T Result { get; protected set; }
}

public abstract class Movement : Recyclable
{
    protected float NowDisplay => ConsoleMath.Round(Velocity.Group.Now.TotalSeconds, 2);
    public Velocity Velocity { get; private set; }
    public SpeedEval Speed { get; set; }
    public GameCollider Element => Velocity.Collider as GameCollider;

    private Movement parent;

    private SpeedEval innerSpeedEval;
    protected Movement()
    {

    }

    protected void Bind(Velocity v, SpeedEval innerSpeedVal)
    {
        Velocity = v;
        innerSpeedEval = innerSpeedVal;
        Speed = CalculateEffectiveSpeed;
        v.Collider.OnDisposed(() => TryDispose());
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Velocity = null;
        innerSpeedEval = null;
        Speed = null;
    }



    public float CalculateEffectiveSpeed()
    {
        var desiredSpeed = innerSpeedEval != null ? innerSpeedEval() : 15;
        return desiredSpeed;
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
