namespace klooie.Gaming;
public class VisibilitySense : IWanderSense
{
    private CollisionPrediction LastPrediction { get;  set; }
    public ScoreComponent Measure(Wander wander, Angle angle, TimeSpan stuckTime)
    {
        LastPrediction = LastPrediction ?? new CollisionPrediction();
        CollisionDetector.Predict(wander.Element, angle, wander._Obstacles, wander.Options.Visibility, CastingMode.Precise, wander._Obstacles.Length, LastPrediction);
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

        var ret= ScoreComponent.Create();
        ret.Id = GetType().Name;
        ret.Value = visibilityScore;
        ret.NeedsToBeNormalized = false;
        return ret;
    }
}
