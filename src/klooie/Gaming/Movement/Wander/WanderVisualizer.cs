namespace klooie.Gaming;
public class WanderVisualizer
{
    private Recyclable? highlightLifetime;
    private int baseZIndex;
    private Wander wander;
    public WanderVisualizer(Wander wander, int baseZIndex)
    {
        this.wander = wander;
        this.baseZIndex = baseZIndex;
        wander.OnNewScoresAvailable.Subscribe(OnNewScoresAvailable, wander);
        wander.OnDisposed(highlightLifetime, DisposeHighlight);
    }

    private static void DisposeHighlight(object highlight) => (highlight as Recyclable)?.TryDispose();

    private void OnNewScoresAvailable(List<AngleScore> scores)
    {
        highlightLifetime?.TryDispose();
        highlightLifetime = DefaultRecyclablePool.Instance.Rent();
        var maxScore = scores.Max(s => s.Total);
        var vision = wander.WanderOptions.Vision;
        var moverPosition = wander.WanderOptions.Velocity.Collider.Bounds;
        var currentZIndex = baseZIndex - 1;
        foreach (var score in scores)
        {
            var seen = new HashSet<Loc>();
            var isMax = score.Total == maxScore;
            var lineColor = ColorForScore(score.Total, isMax);
            var lineStart = moverPosition.TopLeft;
            var lineEnd = moverPosition.TopLeft.RadialOffset(score.Angle, vision.Range);
            var bufferLength = ConsoleBitmap.DefineLineBuffered(
                ConsoleMath.Round(lineStart.Left), ConsoleMath.Round(lineStart.Top),
                ConsoleMath.Round(lineEnd.Left), ConsoleMath.Round(lineEnd.Top));
            for (var i = 0; i < bufferLength; i++)
            {
                var point = ConsoleBitmap.LineBuffer[i];
                if (seen.Contains(point)) continue;
                seen.Add(point);
                var lineHighlight = Game.Current.GamePanel.Add(new ConsoleStringRenderer("o".ToConsoleString(lineColor))
                {
                    ZIndex = isMax ? baseZIndex : currentZIndex,
                    Foreground = lineColor,
                    Bounds = new RectF(point.Left, point.Top, 1, 1)
                });
                highlightLifetime.OnDisposed(() => lineHighlight.TryDispose());
            }
            currentZIndex--;
        }
    }

    private static RGB ColorForScore(float score, bool isMax)
    {
        if (isMax) return RGB.Green;

        var neutral = new RGB(128, 128, 128);
        if (score <= 0.5f)
        {
            // Interpolate from Red (low) to Gray (neutral)
            // t = score / 0.5, so t goes from 0 (red) to 1 (neutral)
            float t = score / 0.5f;
            return InterpolateColor(RGB.Red, neutral, t);
        }
        else
        {
            // Interpolate from Gray (neutral) to Green (high)
            // t = (score - 0.5) / 0.5, so t goes from 0 (neutral) to 1 (green)
            float t = (score - 0.5f) / 0.5f;
            return InterpolateColor(neutral, RGB.Green, t);
        }
    }

    // Simple linear color interpolation between two RGBs
    private static RGB InterpolateColor(RGB from, RGB to, float t)
    {
        byte r = (byte)Math.Round(from.R + (to.R - from.R) * t);
        byte g = (byte)Math.Round(from.G + (to.G - from.G) * t);
        byte b = (byte)Math.Round(from.B + (to.B - from.B) * t);
        return new RGB(r, g, b);
    }
}
