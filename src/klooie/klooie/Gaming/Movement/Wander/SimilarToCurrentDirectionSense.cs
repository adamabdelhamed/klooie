namespace klooie.Gaming;

public class SimilarToCurrentDirectionSense : IWanderSense
{
    private List<Angle> previousAngles = new List<Angle>();

    public ScoreComponent Measure(Wander wander, Angle angle)
    {
        if (wander._LastGoodAngle.HasValue == false)
        {
            return new ScoreComponent()
            {
                Id = "LAST",
                Value = 0,
                Weight = 0
            };
        }

        previousAngles.Add(wander._LastGoodAngle.Value);
        while (previousAngles.Count > 6)
        {
            previousAngles.RemoveAt(0);
        }

        var diff = wander._LastGoodAngle.Value.DiffShortest(angle);

        float score = (90 - diff) / 90;

        var weightMultiplier = 1;
        if (score < 0 && IsFlipFlopping(wander, angle))
        {
            score = -1;
            weightMultiplier = 10;
        }


        return new ScoreComponent()
        {
            Id = "LAST",
            Value = score,
            NeedsToBeNormalized = false,
            WeightBoostMultiplier = weightMultiplier
        };
    }

    private bool IsFlipFlopping(Wander wander, Angle proposedAngle)
    {
        var distinctPreviousAngles = previousAngles.Distinct();

        if (distinctPreviousAngles.Count() == 1 && distinctPreviousAngles.First().Opposite() == proposedAngle)
        {
            return true;
        }

        if (distinctPreviousAngles.Count() != 2) return false;

        if (distinctPreviousAngles.First().Opposite() != distinctPreviousAngles.Last()) return false;

        if (distinctPreviousAngles.Contains(proposedAngle) && proposedAngle != wander._LastGoodAngle)
        {
            return true;
        }

        return false;
    }
}
