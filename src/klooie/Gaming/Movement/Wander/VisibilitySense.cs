namespace klooie.Gaming;
public class VisibilitySense : IWanderSense
{
    private CollisionPrediction LastPrediction { get;  set; }

    private int _measureCount;
    private const int VisibilityThrottle = 100;
    private float cachedVisibilityScore = 0;
    public ScoreComponent Measure(Wander wander, Angle angle, TimeSpan stuckTime)
    {
        if (++_measureCount % VisibilityThrottle != 0)
        {
            var cached = ScoreComponent.Create();
            cached.Id = GetType().Name;
            cached.Value = cachedVisibilityScore;
            cached.NeedsToBeNormalized = false;
            return cached;
        }
        LastPrediction = LastPrediction ?? new CollisionPrediction();
        CollisionDetector.Predict(wander.Element, angle, wander._Obstacles.WriteableBuffer, wander.Options.Visibility, CastingMode.Precise, wander._Obstacles.WriteableBuffer.Count, LastPrediction);
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
        cachedVisibilityScore = ret.Value;
        return ret;
    }
}
