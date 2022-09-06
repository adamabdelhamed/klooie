namespace klooie.Gaming;

public static class SpacialAwarenessEx
{
    private static Random random = new Random();

    public class NudgeEvent
    {
        public GameCollider Element { get; set; }
        public bool Success { get; set; }
    }

    public static Event<NudgeEvent> OnNudge { get; private set; } = new Event<NudgeEvent>();
    public static NudgeEvent NudgeFree(this GameCollider el, RectF? desiredLocation = null, Angle optimalAngle = default)
    {
        var loc = GetNudgeLocation(el, desiredLocation, optimalAngle);
        if (loc.HasValue)
        {
            var elBounds = el.MassBounds;
            var dx = el.Left - elBounds.Left;
            var dy = el.Top - elBounds.Top;
            el.MoveTo(loc.Value.Left + dx, loc.Value.Top + dy);
            var ev = new NudgeEvent() { Element = el, Success = true };
            OnNudge.Fire(ev);
            return ev;
        }
        else
        {
            var ev = new NudgeEvent() { Element = el, Success = false };
            OnNudge.Fire(ev);
            return ev;
        }
    }


    public static LocF? GetNudgeLocation(this GameCollider el, RectF? desiredLoc = null, Angle optimalAngle = default)
    {
        var desiredLocation = desiredLoc.HasValue ? desiredLoc.Value : el.MassBounds;
        var obstacles = el.GetObstacles();
        if (obstacles.Where(o => o.Bounds.Touches(desiredLocation)).Any() || Game.Current.GamePanel.Contains(desiredLocation) == false)
        {
            foreach (var angle in SpacialAwareness.Enumerate360Angles(optimalAngle))
            {
                for (var d = .1f; d < 15f; d += .1f)
                {
                    var testLoc = desiredLocation.OffsetByAngleAndDistance(optimalAngle, d);
                    var testArea = new RectF(testLoc.Left, testLoc.Top, desiredLocation.Width, desiredLocation.Height);
                    if (obstacles.Where(o => o.Bounds.Touches(testArea)).None() && Game.Current.GamePanel.Contains(testArea))
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




    public static async Task<bool> Absorb(GameCollider element, ICollider target, float duration = 400, float cancelDistance = 0f)
    {
        var targetLifetime = (target as ILifetime) ?? Game.Current;
        var originalD = element.Bounds.Center.CalculateDistanceTo(target.Bounds.Center);
        var ret = true;
        await Animator.AnimateAsync(new FloatAnimatorOptions()
        {
            Duration = duration,
            EasingFunction = Animator.EaseOutSoft,
            From = 0,
            To = 1,
            IsCancelled = () => element.IsExpired || target == null || targetLifetime.IsExpired,
            Setter = percentage =>
            {
                if (element.IsExpired || target == null || targetLifetime.IsExpired) return;
                var atLeastD = originalD - (percentage * originalD);

                var currentD = element.Bounds.Center.CalculateDistanceTo(target.Bounds.Center);

                if (cancelDistance > 0 && currentD >= cancelDistance)
                {
                    ret = false;
                    return;
                }


                var delta = (currentD - atLeastD) + .2f;
                var dest = element.Bounds.OffsetByAngleAndDistance(element.Bounds.CalculateAngleTo(target.Bounds), delta);
                if (dest.Center.CalculateDistanceTo(target.Bounds.Center) < currentD)
                {
                    element.MoveTo(dest.Left - element.Width / 2, dest.Top - element.Height / 2);
                }

            }
        });
        return element.IsExpired == false && ret;
    }



    public static int CountObstacles(GameCollider from, GameCollider to, List<GameCollider> obstacles)
    {
        var count = 0;
        var distance = from.MassBounds.CalculateDistanceTo(to.MassBounds) * 3f;
        var angle = from.MassBounds.CalculateAngleTo(to.MassBounds);
        for (var dPrime = .5f; dPrime < distance; dPrime += .5f)
        {
            var testArea = from.MassBounds.OffsetByAngleAndDistance(angle, dPrime);

            var toHit = to.Bounds.Touches(testArea);
            if (toHit)
            {
                return count;
            }

            var obstacleHit = obstacles.Where(o => o.Bounds.Touches(testArea)).FirstOrDefault();
            if (obstacleHit != null)
            {
                count++;
            }

            var d2 = testArea.CalculateDistanceTo(to.MassBounds);
        }

        return count;
    }


    public static IEnumerable<RectF> GetStandardSpots(ConsoleControl element, float granularity, float x = 0, float y = 0) => GetStandardSpots(element.Width, element.Height, granularity, x: x, y: y);

    public static IEnumerable<RectF> GetStandardSpots(float w, float h, float granularity, float x = 0, float y = 0)
    {
        for (var xp = 0f; xp < Game.Current.GamePanel.Width; xp += granularity)
        {
            for (var yp = 0f; yp < Game.Current.GamePanel.Height; yp += granularity / 2f)
            {
                var test = new RectF(x + xp, y + yp, w, h);
                yield return test;
            }
        }
    }



    private static IEnumerable<RectF> GetRandomSpots(ConsoleControl element, float granularity, float x = 0, float y = 0)
    {
        var standard = GetStandardSpots(element, granularity, x, y).ToList();
        while (standard.Count > 0)
        {
            var i = random.Next(0, standard.Count);
            yield return standard[i];
            standard.RemoveAt(i);
        }
    }



    public static bool TrySpawnInGoodSpot(this GameCollider element, Func<RectF, float> ranking, float granularity = 1, float x = 0, float y = 0)
    {
        var obstacles = element.GetObstacles();

        List<Tuple<RectF, float>> scores = new List<Tuple<RectF, float>>();
        foreach (var test in GetRandomSpots(element, granularity, x, y))
        {
            if (obstacles.Where(o => o.Bounds.Touches(test)).Any())
            {
                continue;
            }
            else
            {
                scores.Add(new Tuple<RectF, float>(test, ranking(test)));
            }
        }
        var bestScore = scores.Where(s => s.Item2 != float.MaxValue).OrderBy(s => s.Item2).FirstOrDefault();
        if (bestScore != null)
        {
            element.MoveTo(bestScore.Item1.Left, bestScore.Item1.Top);
            return true;
        }
        else
        {
            return false;
        }
    }



    public static RectF GetRelativePosition(this GameCollider me, RectF other, Angle angle, float distance)
    {

        RectF dest;
        if (distance > 0)
        {
            var rounded = new RectF(ConsoleMath.Round(me.Bounds.Left), ConsoleMath.Round(me.Bounds.Top), me.Bounds.Width, me.Bounds.Height);
            var spot = rounded.Center.OffsetByAngleAndDistance(me.Velocity.Angle.RoundAngleToNearest(90).Add(angle), distance);
            dest = new RectF(spot.Left - other.Width / 2, spot.Top - other.Height / 2, other.Width, other.Height);
        }
        else if (distance == 0)
        {
            var rounded = new RectF(ConsoleMath.Round(me.Bounds.Left), ConsoleMath.Round(me.Bounds.Top), me.Bounds.Width, me.Bounds.Height);
            dest = new RectF(rounded.CenterX - other.Width / 2, rounded.CenterY - other.Height / 2, other.Width, other.Height);
        }
        else
        {
            throw new ArgumentException($"{nameof(distance)} cannot be negative");
        }

        return dest;

    }

}
