namespace klooie.Theming;

public enum EpicTransitionKind
{
    Radial,
    WipeRight,
    WipeDown,
}

public class EpicThemeTransition
{
    internal static async Task Apply(ConsolePanel root, Action application, EpicTransitionKind kind = EpicTransitionKind.Radial)
    {
        // make sure we know which panel we're dealing with
        root = root ?? ConsoleApp.Current.LayoutRoot;

        // create a bitmap of the current screen
        var bitmapOfOldTheme = root.Bitmap.Clone();
        // apply the new theme, but it will be masked by the bitmap, allowing us to create an epic transition
        application();
        root.Paint();
        // grab the new bitmap
        var bitmapOfNewTheme = root.Bitmap.Clone();
        // mast the root panel
        var mask = root.Add(new BitmapControl(bitmapOfOldTheme) { ZIndex = int.MaxValue, AutoSize = true });


        if (kind == EpicTransitionKind.Radial)
        {
            await RadialTransition(bitmapOfOldTheme, bitmapOfNewTheme, mask.Bitmap);
        }
        else if(kind == EpicTransitionKind.WipeRight)
        {
            await WipeRightTransition(bitmapOfOldTheme, bitmapOfNewTheme, mask.Bitmap);
        }
        else if (kind == EpicTransitionKind.WipeDown)
        {
            await WipeDownTransition(bitmapOfOldTheme, bitmapOfNewTheme, mask.Bitmap);
        }
        else
        {
            throw new NotSupportedException($"Transition kind {kind} is not supported");
        }

        // remove the mask and reveal the new themed controls
        mask.Dispose();
    }
    private static Task RadialTransition(ConsoleBitmap bitmapOfOldTheme, ConsoleBitmap bitmapOfNewTheme, ConsoleBitmap mask) =>
        GroupedTransition(bitmapOfOldTheme, bitmapOfNewTheme, mask, p => ConsoleMath.Round(p.distanceFromCenter));

    private static Task WipeRightTransition(ConsoleBitmap bitmapOfOldTheme, ConsoleBitmap bitmapOfNewTheme, ConsoleBitmap mask) =>
        GroupedTransition(bitmapOfOldTheme, bitmapOfNewTheme, mask, p => ConsoleMath.Round(p.x));

    private static Task WipeDownTransition(ConsoleBitmap bitmapOfOldTheme, ConsoleBitmap bitmapOfNewTheme, ConsoleBitmap mask) =>
        GroupedTransition(bitmapOfOldTheme, bitmapOfNewTheme, mask, p => ConsoleMath.Round(p.y));

    private static async Task GroupedTransition(ConsoleBitmap bitmapOfOldTheme, ConsoleBitmap bitmapOfNewTheme, ConsoleBitmap mask, Func<(int x, int y, ConsoleCharacter mine, ConsoleCharacter other, float distanceFromCenter), int> groupBy)
    {
        List<Task> tasks = new List<Task>();
        foreach (var group in EnumeratePixels(bitmapOfOldTheme, bitmapOfNewTheme).GroupBy(groupBy))
        {
            // capture loop variables because we're going to use them in a closure
            var myGroup = group;
            var delay = ConsoleMath.Round(125 * Math.Pow(myGroup.Key, .5f));

            tasks.Add(ConsoleApp.Current.InvokeAsync(async () =>
            {
                if (delay > 0) await Task.Delay(delay);
                await Animator.AnimateAsync(new RGBAnimationOptions()
                {
                    Transitions = group.SelectMany(pixel => new KeyValuePair<RGB, RGB>[]
                    {
                            new KeyValuePair<RGB, RGB>(bitmapOfOldTheme.GetPixel(pixel.x, pixel.y).ForegroundColor, bitmapOfNewTheme.GetPixel(pixel.x, pixel.y).ForegroundColor),
                            new KeyValuePair<RGB, RGB>(bitmapOfOldTheme.GetPixel(pixel.x, pixel.y).BackgroundColor, bitmapOfNewTheme.GetPixel(pixel.x, pixel.y).BackgroundColor),
                    }).ToList(),
                    OnColorsChanged = (colors) =>
                    {
                        for (int i = 0; i < colors.Length; i += 2)
                        {
                            var color = colors[i];
                            var pixel = myGroup.ElementAt(i / 2);
                            mask.SetPixel(pixel.x, pixel.y, new ConsoleCharacter(mask.GetPixel(pixel.x, pixel.y).Value, colors[i], colors[i + 1]));
                        }
                    },
                    Duration = 600,
                    EasingFunction = EasingFunctions.Linear,
                });
            }));
        }
        await Task.WhenAll(tasks);
    }

    private static IEnumerable<(int x, int y, ConsoleCharacter mine, ConsoleCharacter other, float distanceFromCenter)> EnumeratePixels(ConsoleBitmap bitmapOfOldTheme, ConsoleBitmap bitmapOfNewTheme)
    {
        var center = new RectF(0, 0, bitmapOfNewTheme.Width, bitmapOfNewTheme.Height).Center;

        for (var x = 0; x < bitmapOfOldTheme.Width; x++)
        {
            for (var y = 0; y < bitmapOfOldTheme.Height; y++)
            {
                var mine = bitmapOfOldTheme.GetPixel(x, y);
                var other = bitmapOfNewTheme.GetPixel(x, y);
                var distanceFromCenter = center.CalculateNormalizedDistanceTo(new LocF(x, y));
                yield return (x, y, mine, other, distanceFromCenter);
            }
        }
    }
}
