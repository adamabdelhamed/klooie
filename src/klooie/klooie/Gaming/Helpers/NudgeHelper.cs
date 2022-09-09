namespace klooie.Gaming;
public static class NudgeHelper
{
    public static bool NudgeFree(this GameCollider el, RectF? desiredLocation = null, Angle optimalAngle = default, float maxSearch = 15f)
    {
        var loc = GetNudgeLocation(el, desiredLocation, optimalAngle, maxSearch);
        if (loc.HasValue)
        {
            var elBounds = el.MassBounds;
            var dx = el.Left - elBounds.Left;
            var dy = el.Top - elBounds.Top;
            el.MoveTo(loc.Value.Left + dx, loc.Value.Top + dy);
            return true;
        }

        return false;
    }

    private static LocF? GetNudgeLocation(this GameCollider el, RectF? desiredLoc = null, Angle optimalAngle = default, float maxSearch = 15f)
    {
        var desiredLocation = desiredLoc.HasValue ? desiredLoc.Value : el.MassBounds;
        var obstacles = el.GetObstacles();
        if (obstacles.Where(o => o.Bounds.Touches(desiredLocation)).Any())
        {
            foreach (var angle in SpacialAwareness.Enumerate360Angles(optimalAngle))
            {
                for (var d = .1f; d < maxSearch; d += .1f)
                {
                    var testLoc = desiredLocation.OffsetByAngleAndDistance(optimalAngle, d);
                    var testArea = new RectF(testLoc.Left, testLoc.Top, desiredLocation.Width, desiredLocation.Height);
                    if (obstacles.Where(o => o.Bounds.Touches(testArea)).None())
                    {
                        return testLoc.TopLeft;
                    }
                }
            }
            return null;
        }
        else
        {
            return el.Bounds.TopLeft;
        }
    }
}
