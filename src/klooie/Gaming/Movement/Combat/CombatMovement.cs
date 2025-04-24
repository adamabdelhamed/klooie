namespace klooie.Gaming;

 
public abstract class CombatMovement<T> : Movement<T>
{
    public GameCollider Character { get; protected set; }
    public Targeting Targeting { get; protected set; }

    protected CombatMovement(GameCollider c, Targeting targeting, SpeedEval speed) : base()
    {
        this.Character = c;
        this.Targeting = targeting;
    }
}

public abstract class CombatMovement : Movement
{
    public GameCollider Character { get; protected set; }
    public Targeting Targeting { get; protected set; }

    protected CombatMovement(GameCollider c, Targeting targeting, SpeedEval speed) : base()
    {
        this.Character = c;
        this.Targeting = targeting;
        Bind(c.Velocity, speed);
    }


    protected async Task StayOnTarget(float closeEnough)
    {
        if (Targeting.CurrentTargetLifetime == null || Targeting.Target == null) return;
        var lease = this.Lease;
        var characterLease = Character.Lease;
        var targetLifetime = Targeting.CurrentTargetLifetime;
        var targetLifetimeLease = targetLifetime.Lease;

        while (IsStillValid(lease) && targetLifetime.IsStillValid(targetLifetimeLease) && Character.IsStillValid(characterLease))
        {
            var currentDistance = Targeting.Target.Bounds.CalculateDistanceTo(Character);
            Character.Velocity.Angle = Character.CalculateAngleTo(Targeting.Target.Bounds);
            Character.Velocity.Speed = currentDistance < closeEnough ? 0 : Speed();
            await Game.Current.Delay(25);
        }
    }
}
