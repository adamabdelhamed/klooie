namespace klooie.Gaming;


public class ShortCircuitException : Exception { }
public class Mover
{
    public const float DefaultCloseEnough = .8f;

    public static async Task Invoke(Movement parent, Movement process) => await process.InvokeInternal(parent);
    public static async Task<T> InvokeGet<T>(Movement parent, Movement<T> process) { await Invoke(parent, process); return process.Result; }
    public static async Task<T> InvokeGetWithShortCircuit<T>(Movement<T> process) { await InvokeWithShortCircuit(process); return process.Result; }
    public static async Task<T> InvokeGetOrTimeout<T>(Movement parent, Movement<T> process, ILifetimeManager timeout) { await InvokeOrTimeout(parent, process, timeout); return process.Result; }
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
        return InvokeOrTimeout(parent, process, Lifetime.EarliestOf(parent, process, Game.Current.Delay(timeout).ToLifetime()));
    }

    public static async Task<bool> InvokeOrTimeout(Movement parent, Movement process, ILifetimeManager maxLifetime)
    {
        try
        {
            maxLifetime.OnDisposed(() => process.TryDispose());
            await process.InvokeInternal(parent);
            return true;
        }
        catch (ShortCircuitException)
        {
            if (maxLifetime.IsExpired)
            {
                return false;
            }
            else
            {
                throw;
            }
        }
    }

    public static async Task<bool> InvokeOrTimeout(Movement process, ILifetimeManager maxLifetime)
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
    protected Movement(Velocity v, SpeedEval speed) : base(v, speed) { }
    public abstract T Result { get; protected set; }
}

public abstract class Movement : Lifetime
{
    protected float NowDisplay => ConsoleMath.Round(Velocity.Group.Now.TotalSeconds, 2);
    public Velocity Velocity { get; private set; }
    public SpeedEval Speed { get; set; }
    public GameCollider Element => Velocity.Collider as GameCollider;

    private Movement parent;

    private SpeedEval innerSpeedEval;
    protected Movement(Velocity v, SpeedEval innerSpeedEval)
    {
        Velocity = v;
        this.innerSpeedEval = innerSpeedEval;
        Speed = CalculateEffectiveSpeed;
        v.Collider.OnDisposed(() => TryDispose());
    }



    public float CalculateEffectiveSpeed()
    {
        var desiredSpeed = innerSpeedEval != null ? innerSpeedEval() : 15;
        return desiredSpeed;
    }

    protected abstract Task Move();

    internal async Task InvokeInternal(Movement parent)
    {
        if (IsExpired)
        {
            throw new NotSupportedException("Movement processes can only run once");
        }
        try
        {
            this.parent = parent;
            await AssertAlive();
            await Move();
            await AssertAlive();
        }
        finally
        {
            TryDispose();
        }
    }

    public async Task AssertAlive()
    {
        if (IsExpired)
        {
            throw new ShortCircuitException();
        }

        await OnAlive();

        if (IsExpired)
        {
            throw new ShortCircuitException();
        }

        if (parent != null)
        {
            await parent.AssertAlive();
        }
    }

    protected virtual Task OnAlive() => Task.CompletedTask;


    private async Task PreDelay()
    {
        await AssertAlive();
    }

    private async Task PostDelay()
    {
        await AssertAlive();
    }

    protected async Task Delay(double ms)
    {
        await PreDelay();
        await Game.Current.Delay(ms);
        await PostDelay();
    }

    protected async Task Delay(TimeSpan timeout)
    {
        await PreDelay();
        await Game.Current.Delay(timeout);
        await PostDelay();
    }

    protected async Task DelayFuzzyAsync(float ms, float maxDeltaPercentage = 0.1f)
    {
        await PreDelay();
        await Game.Current.DelayFuzzy(ms, maxDeltaPercentage);
        await PostDelay();
    }

    protected async Task YieldAsync()
    {
        await PreDelay();
        await Task.Yield();
        await PostDelay();
    }
}
