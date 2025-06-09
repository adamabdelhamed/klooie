namespace klooie.Gaming;

public class ChargeOptions : CombatMovementOptions
{
    public RectF? DefaultCuriosityPoint { get; set; }
    public float CloseEnough { get; set; } = 5;
    /// <summary>
    /// Returns an enemy in the immediate area, if any. Returns null if none are found.
    /// </summary>
    public Func<RectF?>? PrioritizedPointOfInterest { get; set; }
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
                CuriousityPoint = CalculateCuriosityPoint,
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

    private RectF? CalculateCuriosityPoint()
    {
        var pri0 = ChargeOptions.Targeting.Target?.Bounds;
        if(pri0.HasValue) return pri0.Value;

        var pri1 = ChargeOptions.PrioritizedPointOfInterest?.Invoke();
        if(pri1.HasValue) return pri1.Value;

        var pri2 = ChargeOptions.DefaultCuriosityPoint;
        if (pri2.HasValue) return pri2.Value;

        var pri3 = Game.Current?.GameBounds.Center.ToRect(10, 5);
        if (pri3.HasValue) return pri3.Value;

        return null;
    }
}
