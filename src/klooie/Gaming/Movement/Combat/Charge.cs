namespace klooie.Gaming;

public class ChargeOptions : CombatMovementOptions
{
    public required ICollidable DefaultCuriosityPoint { get; set; }
    public float CloseEnough { get; set; } = 5;
}

public class Charge : CombatMovement
{
    public ChargeOptions ChargeOptions => (ChargeOptions)Options;

    public Charge() { }
    public static Movement Create(ChargeOptions options)
    {
        var c = ChargePool.Instance.Rent();
        c.Bind(options);
        return c;
    }
    protected override async Task Move()
    {
        var lease = Lease;
        var lt = UntilCloseToTargetLifetime.Create(Options.Velocity.Collider, ChargeOptions.Targeting, ChargeOptions.CloseEnough);
        Game.Current.OnDisposed(() => lt.TryDispose());
        try
        {
            while (this.IsStillValid(lease))
            {
                await Mover.InvokeOrTimeout(this, Wander.Create(new WanderOptions()
                {
                    CuriousityPoint = () => ChargeOptions.Targeting.Target ?? ChargeOptions.DefaultCuriosityPoint,
                    CloseEnough = ChargeOptions.CloseEnough,
                    Speed = ChargeOptions.Speed,
                    Velocity = Options.Velocity,
                    Vision = ChargeOptions.Vision,
                }), lt);

                await StayOnTarget(ChargeOptions.CloseEnough);
                await Task.Yield();
            }
        }
        finally
        {
            lt.TryDispose();
        }
    }
}
