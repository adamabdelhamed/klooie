namespace klooie.Gaming;

    public class CloserToTargetSense : IWanderSense
    {
        public ScoreComponent Measure(Wander wander, Angle angle, TimeSpan stuckTime)
        {
            float doesItGetMeCloserScore = 0;
            //score component - If I have a curiosity point then does this angle get me closer or farther?

            var dt = (float)(wander.Speed() * Game.Current.MainColliderGroup.LatestDT);
            if (wander._CuriosityPoint != null)
            {
                var castArea = wander.Element.Bounds.RadialOffset(angle, 1f);
                var dNow = wander.Element.Bounds.CalculateNormalizedDistanceTo(wander._CuriosityPoint.Bounds);
                var dWhatIf = castArea.CalculateNormalizedDistanceTo(wander._CuriosityPoint.Bounds);
                doesItGetMeCloserScore = (dNow - dWhatIf) / dt;
                if (doesItGetMeCloserScore < 0)
                {
                    doesItGetMeCloserScore = -(float)Math.Pow(Math.Abs(doesItGetMeCloserScore), .5);
                }
            }

            return new ScoreComponent()
            {
                Id = GetType().Name,
                Value = doesItGetMeCloserScore,
            };
        }
    }

