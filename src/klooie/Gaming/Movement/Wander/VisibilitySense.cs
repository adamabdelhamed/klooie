namespace klooie.Gaming;
public class VisibilitySense : IWanderSense
{
    public CollisionPrediction LastPrediction { get; private set; }
    public ScoreComponent Measure(Wander wander, Angle angle, TimeSpan stuckTime)
    {
        LastPrediction = CollisionDetector.Predict(wander.Element, angle, wander._Obstacles, wander.Options.Visibility, CastingMode.Precise);
        var visibilityScore = 0f;
        if (LastPrediction.CollisionPredicted == false)
        {
            visibilityScore = 1;
        }
        else
        {
            var eb = wander.Element.Bounds;
            visibilityScore = LocF.CalculateNormalizedDistanceTo(eb.Left, eb.Top, LastPrediction.LKGX, LastPrediction.LKGY) / wander.Options.Visibility;
            visibilityScore = Math.Min(visibilityScore, 1);
        }

        return new ScoreComponent()
        {
            Id = GetType().Name,
            Value = visibilityScore,
            NeedsToBeNormalized = false,
        };
    }
}
