namespace klooie.Gaming;

 
public class CombatMovementOptions : MovementOptions
{
    public required Targeting Targeting { get; set; }
}

 
public abstract class CombatMovement : Movement
{
    public CombatMovementOptions CombatOptions => (CombatMovementOptions)Options;

    protected async Task StayOnTarget(float closeEnough)
    {
        if (CombatOptions.Targeting.CurrentTargetLifetime == null || CombatOptions.Targeting.Target == null) return;
        var lease = this.Lease;
        var characterLease = Options.Velocity.Collider.Lease;
        var targetLifetime = CombatOptions.Targeting.CurrentTargetLifetime;
        var targetLifetimeLease = targetLifetime.Lease;

        while (IsStillValid(lease) && targetLifetime.IsStillValid(targetLifetimeLease) && Options.Velocity.Collider.IsStillValid(characterLease))
        {
            var currentDistance = CombatOptions.Targeting.Target.Bounds.CalculateDistanceTo(Options.Velocity.Collider);
            Options.Velocity.Collider.Velocity.Angle = Options.Velocity.Collider.CalculateAngleTo(CombatOptions.Targeting.Target.Bounds);
            Options.Velocity.Collider.Velocity.Speed = currentDistance < closeEnough ? 0 : Options.Speed();
            await Game.Current.Delay(25);
        }
    }
}
