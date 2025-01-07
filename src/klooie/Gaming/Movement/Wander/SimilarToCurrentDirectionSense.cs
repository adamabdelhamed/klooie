namespace klooie.Gaming;

public class SimilarToCurrentDirectionSense : IWanderSense
{
    private List<Angle> previousAngles = new List<Angle>();

    public ScoreComponent Measure(Wander wander, Angle angle, TimeSpan stuckTime)
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
        // Fixed-size structure to track up to two distinct angles
        Angle? firstAngle = null;
        Angle? secondAngle = null;

        for (int i = 0; i < previousAngles.Count; i++)
        {
            var angle = previousAngles[i];

            if (firstAngle == null)
            {
                firstAngle = angle;
            }
            else if (firstAngle != angle)
            {
                if (secondAngle == null)
                {
                    secondAngle = angle;
                }
                else if (secondAngle != angle)
                {
                    // More than two distinct angles found
                    return false;
                }
            }
        }

        // Handle case where there is only one distinct angle
        if (secondAngle == null)
        {
            return firstAngle?.Opposite() == proposedAngle;
        }

        // Ensure the two distinct angles are opposites
        if (firstAngle?.Opposite() != secondAngle)
        {
            return false;
        }

        // Check if the proposed angle is in the set and isn't the last good angle
        if ((firstAngle == proposedAngle || secondAngle == proposedAngle) && proposedAngle != wander._LastGoodAngle)
        {
            return true;
        }

        return false;
    }
}
