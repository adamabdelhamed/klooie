namespace klooie;

public static class AnimateEx
{
    public static async Task<AnimationFilter> FadeAnimate(this ConsoleControl c, RGB from, RGB to, float duration = 500, EasingFunction easingFunction = null, float fromPerecntage = 0, float toPercentage = 1)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = new AnimationFilter() { From = from, To = to };
        c.Filters.Add(filter);

        await Animator.AnimateAsync(Animator.FloatAnimationState.Create(fromPerecntage, toPercentage, duration, p => filter.Percentage = p, easingFunction, null, false, 0, null, null));
        return filter;
    }
}
public sealed class AnimationFilter : IConsoleControlFilter
{
    public float Percentage { get; set; }
    public RGB From { get; set; }
    public RGB To { get; set; }

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }

    public void Filter(ConsoleBitmap bitmap)
    {
        var targetColor = From.ToOther(To, Percentage);
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);

                if (pixel.BackgroundColor != pixel.ForegroundColor && pixel.BackgroundColor == RGB.Black && pixel.Value != ' ')
                {
                    bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, targetColor));
                }

                if (pixel.BackgroundColor != RGB.Black)
                {
                    bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, pixel.ForegroundColor, targetColor));
                }
            }
        }
    }
}