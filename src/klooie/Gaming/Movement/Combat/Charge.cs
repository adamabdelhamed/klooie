namespace klooie.Gaming;

public class Charge : CombatMovement
{
    public float CloseEnough { get; set; } = 5;
    private ICollidable defaultCuriosityPoint;
    private Charge(GameCollider c, Targeting targeting, SpeedEval speed, ICollidable defaultCuriosityPoint) : base(c, targeting, speed) 
    {
        this.defaultCuriosityPoint = defaultCuriosityPoint;
    }
    public static Movement Create(GameCollider c, Targeting targeting, SpeedEval speed, ICollidable defaultCuriosityPoint) => new Charge(c, targeting, speed, defaultCuriosityPoint);
    protected override async Task Move()
    {
        var lease = Lease;
        var lt = UntilCloseToTargetLifetime.Create(Character, Targeting, CloseEnough);
        Game.Current.OnDisposed(() => lt.TryDispose());
        try
        {
            while (this.IsStillValid(lease))
            {
                await Mover.InvokeOrTimeout(this, Wander.Create(Velocity, Speed, new WanderOptions()
                {
                    CuriousityPoint = () => Targeting.Target ?? defaultCuriosityPoint,
                }), lt);

                await StayOnTarget(CloseEnough);
                await Task.Yield();
            }
        }
        finally
        {
            lt.TryDispose();
        }
    }
}
