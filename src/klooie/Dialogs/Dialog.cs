namespace klooie;

/// <summary>
/// Base class for all dialogs
/// </summary>
public class DialogOptions
{
    /// <summary>
    /// Optionally set the parent otherwise the app's root will be used
    /// </summary>
    public ConsolePanel? Parent { get; set; }

    /// <summary>
    /// When set to true the dialog will push the focus stack when
    /// the dialog opens and pop the stack with it closes. This ensures
    /// that the focus cycling via the tab key only cycles through focusable
    /// controls on the dialog and skips the focusable controls that are
    /// underneath the dialog.
    /// </summary>
    public bool PushPop { get; set; } = true;

    /// <summary>
    /// Increase to slow the animation. Decrease to speed it up.
    /// Set to zero to skip animation.
    /// </summary>
    public float SpeedPercentage { get; set; } = 1;
    
    /// <summary>
    /// If true allows the user to close the dialog via the escape key
    /// </summary>
    public bool AllowEscapeToClose { get; set; }


    /// <summary>
    /// optionally sets the z-index of the control
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// optionally sets the border color of the dialog (defaults to a naturally contrasting color)
    /// </summary>
    public RGB? BorderColor { get; set; }

    /// <summary>
    /// Tags to be added to the root of the dialog
    /// </summary>
    public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();
}

/// <summary>
/// Utility that lets you add animated dialogs to your ConsoleApps.
/// </summary>
public static class Dialog
{
    /// <summary>
    /// A tag that is put on the root border panel
    /// </summary>
    public const string Tag = "Dialog";

    /// <summary>
    /// An event that fires when any dialog is shown. It is fired once the dialog has settled
    /// (i.e. the open animation is completed, the content is visible).
    /// </summary>
    public static Event Shown { get; private set; } = new Event();

    /// <summary>
    /// Shows a dialog
    /// </summary>
    /// <param name="contentFactory">responsible for setting your own width and height</param>
    /// <param name="options">options you can configure</param>
    /// <returns>true if the dialog was cancelled, false otherwise</returns>
    public static async Task<bool> Show(Func<ConsoleControl> contentFactory, DialogOptions options = null)
    {
        var cancelled = false;
        options = options ?? new DialogOptions();
        options.Parent = options.Parent ?? ConsoleApp.Current.LayoutRoot;

        var content = contentFactory();
        var borderColor = options.BorderColor.HasValue ? options.BorderColor : content.Background.Darker;

        if(options.BorderColor.HasValue == false && content.Background == content.Background.Darker)
        {
            borderColor = content.Background.Brighter;
        }

        IConsoleControlFilter[] filtersToAdd = [new ForegroundColorFilter(RGB.Black), new BackgroundColorFilter(RGB.Black)];

        foreach (var item in filtersToAdd)
        {
            content.Filters.Add(item);
        }

        var maxFocusDepth = ConsoleApp.Current.LayoutRoot.Descendents.Select(c => c.FocusStackDepth).DefaultIfEmpty(0).Max();

        var dialogContainer = options.Parent.Add(new BorderPanel(content) { FocusStackDepth = options.PushPop ? maxFocusDepth + 1 : maxFocusDepth, BorderColor = borderColor, Background = content.Background, Width = 1, Height = 1, ZIndex = options.ZIndex }).CenterBoth();
        dialogContainer.AddTag(Tag);
        options.Tags?.ForEach(t => dialogContainer.AddTag(t));


        if (options.AllowEscapeToClose) ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, () => { cancelled = true; content.Dispose(); }, content);

        // animate in
        await Forward(300 * options.SpeedPercentage, content, percentage => dialogContainer.Width = Math.Max(1, ConsoleMath.Round((4 + content.Width) * percentage)));
        await Forward(200 * options.SpeedPercentage, content, percentage => dialogContainer.Height = Math.Max(1, ConsoleMath.Round((2 + content.Height) * percentage)));

        foreach (var item in filtersToAdd)
        {
            content.Filters.Remove(item);
        }

        Shown.Fire();

        if (content.ShouldContinue)
        {
            // wait for the content to dispose, which indicates that it's time to close
            await content.AsTask();
        }

        // animate out
        await Reverse(150 * options.SpeedPercentage, content, percentage => dialogContainer.Height = Math.Max(1, (int)Math.Floor((2 + content.Height) * percentage)));
        await Task.Delay((int)(200 * options.SpeedPercentage));
        await Reverse(200 * options.SpeedPercentage, content, percentage => dialogContainer.Width = Math.Max(1, ConsoleMath.Round((4 + content.Width) * percentage)));
        dialogContainer.Dispose();
        await ConsoleApp.Current.RequestPaintAsync();
        return cancelled;
    }

    private static Task Forward(float duration, ILifetime lt, Action<float> setter) => AnimateCommon(duration, lt, setter, 0, 1);
    private static Task Reverse(float duration, ILifetime lt, Action<float> setter) => AnimateCommon(duration, lt, setter, 1, 0);
    private static Task AnimateCommon(float duration, ILifetime lt, Action<float> setter, float from, float to)
    {
        if (duration == 0)
        {
            setter(to);
            return Task.CompletedTask;
        }
        else
        {
            return Animator.AnimateAsync(new FloatAnimationOptions()
            {
                From = from,
                To = to,
                Duration = duration,
                EasingFunction = EasingFunctions.EaseInOut,
                IsCancelled = () => lt.IsExpired,
                Setter = percentage => setter(percentage)
            });
        }
    }
}
