namespace klooie.Theming;
public abstract class EpicThemeTransition
{
    public ConsoleBitmap BitmapOfOldTheme { get; set; }
    public ConsoleBitmap BitmapOfNewTheme { get; set; }
    public ConsoleBitmap Mask { get; set; }
    protected abstract Task Execute();

    public async Task Apply(Theme theme, ConsolePanel root, ILifetime lt = null)
    {
        root = root ?? ConsoleApp.Current.LayoutRoot;
        var bitmapOfOldTheme = root.Bitmap.Clone();
        theme.Apply(root, lt);
        root.Paint();
        var bitmapOfNewTheme = root.Bitmap.Clone();
        var mask = root.Add(new BitmapControl(bitmapOfOldTheme) { ZIndex = int.MaxValue, AutoSize = true });
        BitmapOfOldTheme = bitmapOfOldTheme;
        BitmapOfNewTheme = bitmapOfNewTheme;
        Mask = mask.Bitmap;
        await Execute();
        mask.Dispose();
    }
}

public enum BuiltInEpicThemeTransitionKind
{
    Radial,
    WipeRight,
    WipeDown,
}

public class BuiltInEpicThemeTransition : EpicThemeTransition
{
    protected virtual Func<int, int> DelayFunc { get; set; } = groupKey => ConsoleMath.Round(125 * Math.Pow(groupKey, .5f));
    protected virtual Func<IGrouping<int, (int x, int y, ConsoleCharacter mine, ConsoleCharacter other, float distanceFromCenter)>, double> DurationFunc { get; set; } = group => 0;

    private Func<(int x, int y, ConsoleCharacter mine, ConsoleCharacter other, float distanceFromCenter), int> groupBy;

    public BuiltInEpicThemeTransition(BuiltInEpicThemeTransitionKind kind)
    {
        groupBy = kind == BuiltInEpicThemeTransitionKind.Radial ? pixel => ConsoleMath.Round(pixel.distanceFromCenter) :
            kind == BuiltInEpicThemeTransitionKind.WipeRight ? pixel => pixel.x :
            kind == BuiltInEpicThemeTransitionKind.WipeDown ? pixel => pixel.y :
            throw new NotSupportedException($"Transition kind {kind} is not supported");
    }

    protected override async Task Execute()
    {
        List<Task> tasks = new List<Task>();
        int i = 0;
        foreach (var group in EnumeratePixels(BitmapOfOldTheme, BitmapOfNewTheme).GroupBy(groupBy))
        {
            // capture loop variables because we're going to use them in a closure
            var myGroup = group;
            var delay = DelayFunc(myGroup.Key);
            var myI = i;
            tasks.Add(ConsoleApp.Current.InvokeAsync(async () =>
            {
                if (delay > 0) await Task.Delay(delay);
                var foregroundTransitions = myGroup.SelectMany(pixel => new KeyValuePair<RGB, RGB>[]
                {
                    new KeyValuePair<RGB, RGB>(BitmapOfOldTheme.GetPixel(pixel.x, pixel.y).ForegroundColor, BitmapOfNewTheme.GetPixel(pixel.x, pixel.y).ForegroundColor),
                }).ToList();
                var backgroundTransitions = myGroup.SelectMany(pixel => new KeyValuePair<RGB, RGB>[]
                {
                    new KeyValuePair<RGB, RGB>(BitmapOfOldTheme.GetPixel(pixel.x, pixel.y).BackgroundColor, BitmapOfNewTheme.GetPixel(pixel.x, pixel.y).BackgroundColor),
                }).ToList();
                Action<RGB> foregroundCallback = (color) =>
                {

                    var pixel = myGroup.ElementAt(myI);
                    var maskPixel = Mask.GetPixel(pixel.x, pixel.y);
                    Mask.SetPixel(pixel.x, pixel.y, new ConsoleCharacter(maskPixel.Value, color, maskPixel.BackgroundColor));

                };
                Action<RGB> backgroundCallback = (color) =>
                {
                    var pixel = myGroup.ElementAt(myI);
                    var maskPixel = Mask.GetPixel(pixel.x, pixel.y);
                    Mask.SetPixel(pixel.x, pixel.y, new ConsoleCharacter(maskPixel.Value, maskPixel.ForegroundColor, color));
                };

                var groupTasks = new List<Task>();
                for (var j = 0; j < foregroundTransitions.Count; j++)
                {
                    groupTasks.Add(Animator.AnimateAsync(foregroundTransitions[j].Key, foregroundTransitions[j].Value, DurationFunc(myGroup), foregroundCallback,  EasingFunctions.Linear));
                }
                await Task.WhenAll(groupTasks);
            }));
            i++;
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
