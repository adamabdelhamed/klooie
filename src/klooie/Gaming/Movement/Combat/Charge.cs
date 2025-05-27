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
        var myLease = Lease;
        var targetingLease = ChargeOptions.Targeting.Lease;
        var colliderLease = Options.Velocity.Collider.Lease;
        var velocityLease = Options.Velocity.Lease;
        var visionLease = ChargeOptions.Vision.Lease;

        bool IsAlive() => IsStillValid(myLease) &&
                          ChargeOptions?.Targeting?.IsStillValid(targetingLease) == true &&
                          Options?.Velocity?.IsStillValid(velocityLease) == true &&
                          Options?.Velocity?.Collider?.IsStillValid(colliderLease) == true &&
                          ChargeOptions?.Vision?.IsStillValid(visionLease) == true;

        var wanderOptions = new WanderOptions()
        {
            CuriousityPoint = () => ChargeOptions.Targeting.Target ?? ChargeOptions.DefaultCuriosityPoint,
            CloseEnough = ChargeOptions.CloseEnough,
            Speed = ChargeOptions.Speed,
            Velocity = Options.Velocity,
            Vision = ChargeOptions.Vision,
        };

        while (IsAlive())
        {
            var lt = UntilCloseToTargetLifetime.Create(Options.Velocity.Collider, ChargeOptions.Targeting, ChargeOptions.CloseEnough);
            this.OnDisposed(() => lt.TryDispose());
            await Mover.InvokeOrTimeout(this, Wander.Create(wanderOptions), lt);
            await StayOnTarget(ChargeOptions.CloseEnough);
            await Task.Yield();
        }
    }
}
