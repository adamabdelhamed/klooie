namespace klooie.Gaming;

public class ChargeOptions : CombatMovementOptions
{
    public required ICollidable DefaultCuriosityPoint { get; set; }
    public float CloseEnough { get; set; } = 5;
    /// <summary>
    /// Returns an enemy in the immediate area, if any. Returns null if none are found.
    /// </summary>
    public Func<ICollidable?>? NearbyEnemyProvider { get; set; }
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
        var delayState = DelayState.Create(this);
        try
        {
            delayState.AddDependency(ChargeOptions.Targeting);
            delayState.AddDependency(Options.Velocity.Collider);
            delayState.AddDependency(Options.Velocity);
            delayState.AddDependency(ChargeOptions.Vision);

            var wanderOptions = new WanderOptions()
            {
                CuriousityPoint = () =>
                    ChargeOptions.Targeting.Target
                    ?? ChargeOptions.NearbyEnemyProvider?.Invoke()
                    ?? ChargeOptions.DefaultCuriosityPoint,
                CloseEnough = ChargeOptions.CloseEnough,
                Speed = ChargeOptions.Speed,
                Velocity = Options.Velocity,
                Vision = ChargeOptions.Vision,
            };

            while (delayState.AreAllDependenciesValid)
            {
                var lt = UntilCloseToTargetLifetime.Create(Options.Velocity.Collider, ChargeOptions.Targeting, ChargeOptions.CloseEnough);
                var ltLease = lt.Lease;
                this.OnDisposed(() =>
                {
                    if (lt.IsStillValid(ltLease)) lt.TryDispose();
                });
                if (lt.IsStillValid(ltLease))
                {
                    await Mover.InvokeOrTimeout(this, Wander.Create(wanderOptions), lt);
                }
                if (!delayState.AreAllDependenciesValid) break;
                await StayOnTarget(ChargeOptions.CloseEnough);
                await Task.Yield();
            }
        }
        finally
        {
            delayState.TryDispose();
        }
    }
}
