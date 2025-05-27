namespace klooie.Gaming;
public class WanderEvaluator
{
    public float TotalDistanceTravelled { get;private set; }
    public TimeSpan TotalTimeTravelling => Game.Current.MainColliderGroup.Now - startTime;
    private RectF previousBounds;
    private Wander wander;
    private TimeSpan startTime;

    public float AverageSpeed => TotalDistanceTravelled / (float)TotalTimeTravelling.TotalSeconds;

    public WanderEvaluator(Wander wander)
    {
        this.wander = wander;
        startTime = Game.Current.MainColliderGroup.Now;

        previousBounds = wander.WanderOptions.Velocity.Collider.Bounds;
        wander.WanderOptions.Velocity.Collider.BoundsChanged.Subscribe(ColliderMoved, wander);
    }

    private void ColliderMoved()
    {
        var distanceMoved = wander.WanderOptions.Velocity.Collider.Bounds.CalculateNormalizedDistanceTo(previousBounds);
        TotalDistanceTravelled += distanceMoved;
    }
}