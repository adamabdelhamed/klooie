namespace klooie.Gaming;
public interface IWanderSense
{
    ScoreComponent Measure(Wander wander, Angle angle, TimeSpan stuckDuration);
}

