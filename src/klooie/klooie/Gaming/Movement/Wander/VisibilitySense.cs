namespace klooie.Gaming;
public class VisibilitySense : IWanderSense
{
    public HitPrediction LastPrediction { get; private set; }
    public ScoreComponent Measure(Wander wander, Angle angle)
    {
        var colliders = wander._Obstacles.ToArray();
        LastPrediction = HitDetection.PredictHit(wander.Element, angle, colliders, wander.Options.Visibility, CastingMode.Rough);
        var visibilityScore = 0f;
        if (LastPrediction.Type == HitType.None)
        {
            visibilityScore = 1;
        }
        else
        {
            var eb = wander.Element.MassBounds;
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
