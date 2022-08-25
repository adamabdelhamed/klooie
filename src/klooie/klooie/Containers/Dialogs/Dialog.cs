using PowerArgs;
namespace klooie;

public class DialogOptions
{
    public ConsolePanel Parent { get; set; }
    public bool PushPop { get; set; } = true;
    public float SpeedPercentage { get; set; } = 1;
    public bool AllowEscapeToClose { get; set; }
    public bool AllowEnterToClose { get; set; }
    public int ZIndex { get; set; }
    public RGB? BorderColor { get; set; }
}

/// <summary>
/// Utility that lets you add animated dialogs to your ConsoleApps.
/// </summary>
public class Dialog
{
    public static Event Shown { get; private set; } = new Event();
    /// <summary>
    /// Shows a dialog
    /// </summary>
    /// <param name="contentFactory">responsible for setting your own width and height</param>
    /// <param name="options">options you can configure</param>
    /// <returns>an async task</returns>
    public static async Task<bool> Show(Func<ConsoleControl> contentFactory, DialogOptions options = null)
    {
        var cancelled = false;
        options = options ?? new DialogOptions();
        options.Parent = options.Parent ?? ConsoleApp.Current.LayoutRoot;

        if (options.PushPop)
        {
            ConsoleApp.Current.PushFocusStack();
        }

        var content = contentFactory();
        var borderColor = options.BorderColor.HasValue ? options.BorderColor : content.Background.Darker;

        if(options.BorderColor.HasValue == false && content.Background == content.Background.Darker)
        {
            borderColor = content.Background.Brighter;
        }

        content.IsVisible = false;
        var dialogContainer = options.Parent.Add(new BorderPanel(content) { BorderColor = borderColor, Background = content.Background, Width = 1, Height = 1, ZIndex = options.ZIndex }).CenterBoth();

        if (options.AllowEscapeToClose) ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, () => { cancelled = true; content.Dispose(); }, content);
        if (options.AllowEnterToClose) ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Enter, () => { cancelled = false; content.Dispose(); }, content);

        // animate in in
        await Forward(300 * options.SpeedPercentage, content, percentage => dialogContainer.Width = Math.Max(1, ConsoleMath.Round((4 + content.Width) * percentage)));
        await Forward(200 * options.SpeedPercentage, content, percentage => dialogContainer.Height = Math.Max(1, ConsoleMath.Round((2 + content.Height) * percentage)));
        content.IsVisible = true;
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

        if(options.PushPop)
        {
            ConsoleApp.Current.PopFocusStack();
        }
        return cancelled;
    }

    private static Task Forward(float duration, Lifetime lt, Action<float> setter) => AnimateCommon(duration, lt, setter, 0, 1);
    private static Task Reverse(float duration, Lifetime lt, Action<float> setter) => AnimateCommon(duration, lt, setter, 1, 0);
    private static async Task AnimateCommon(float duration, Lifetime lt, Action<float> setter, float from, float to)
    {
        if (duration == 0)
        {
            setter(to);
        }
        else
        {
            await Animator.AnimateAsync(new FloatAnimatorOptions()
            {
                From = from,
                To = to,
                Duration = duration,
                EasingFunction = Animator.EaseInOut,
                IsCancelled = () => lt.IsExpired,
                Setter = percentage => setter(percentage)
            });
        }
    }
}
