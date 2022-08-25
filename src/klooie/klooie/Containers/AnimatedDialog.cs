using PowerArgs;
namespace klooie;

public class DialogChoice
{
    /// <summary>
    /// The display text for the option
    /// </summary>
    public ConsoleString DisplayText { get; set; }

    /// <summary>
    /// The id of this option's value
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// An object that this option represents
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// Compares the ids of each option
    /// </summary>
    /// <param name="obj">the other option</param>
    /// <returns>true if the ids match</returns>
    public override bool Equals(object obj)
    {
        var b = obj as DialogChoice;
        if (b == null) return false;
        return b.Id == this.Id;
    }

    /// <summary>
    /// gets the hashcode of the id
    /// </summary>
    /// <returns>the hashcode of the id</returns>
    public override int GetHashCode() => Id == null ? base.GetHashCode() : Id.GetHashCode();

    public static IEnumerable<DialogChoice> OKCancel => new DialogChoice[]
    {
        new DialogChoice(){ DisplayText = "OK".ToConsoleString(), Id = "OK", Value = "OK" },
        new DialogChoice(){ DisplayText = "Cancel".ToConsoleString(), Id = "Cancel", Value = "Cancel" },
    };

    public static IEnumerable<DialogChoice> YesNo => new DialogChoice[]
  {
        new DialogChoice(){ DisplayText = "Yes".ToConsoleString(), Id = "Yes", Value = "Yes" },
        new DialogChoice(){ DisplayText = "No".ToConsoleString(), Id = "No", Value = "No" },
  };

    public static IEnumerable<DialogChoice> Close => new DialogChoice[]
   {
        new DialogChoice(){ DisplayText = "Close".ToConsoleString(), Id = "Close", Value = "Close" },
   };
}

public class AnimatedDialogOptions
{
    public ConsolePanel Parent { get; set; }
    public bool PushPop { get; set; } = true;
    public float SpeedPercentage { get; set; } = 1;
    public bool AllowEscapeToClose { get; set; }
    public bool AllowEnterToClose { get; set; }
    public int ZIndex { get; set; }
    public RGB? BorderColor { get; set; }
}

public abstract class DialogWithChoicesOptions : AnimatedDialogOptions
{
    public bool AutoFocusChoices { get; set; } = true;
    public IEnumerable<DialogChoice> UserChoices { get; set; } = Enumerable.Empty<DialogChoice>();
    public int DialogWidth { get; set; } = ConsoleApp.Current != null ? ConsoleMath.Round(ConsoleApp.Current.Width * .75f) : 80;
    public int DialogHeight { get; set; } = 8;
    public RGB BackgroundColor { get; set; } = ConsoleString.DefaultBackgroundColor;
    public ILifetimeManager? MaxLifetime { get; set; }

    internal abstract ConsoleControl ContentFactory(ConsolePanel contentContainer);
}

public class ShowMessageOptions : DialogWithChoicesOptions
{
    public ConsoleString Message { get; private set; }

    public ShowMessageOptions(ConsoleString msg) => Message = msg;

    public ShowMessageOptions(string msg) => Message = msg.ToConsoleString();

    internal override ConsoleControl ContentFactory(ConsolePanel contentContainer)
    {
        var ret = new Label(LabelRenderMode.MultiLineSmartWrap) { Y = 1, X = 2, Width = contentContainer.Width - 4, Text = Message  };
        return ret;
    }
}

public class ShowTextInputOptions : DialogWithChoicesOptions
{
    public Func<TextBox> TextBoxFactory { get; set; }
    internal ConsoleString Message { get; set; }

    public ConsoleString Value { get; private set; }

    public ShowTextInputOptions()
    {
        AutoFocusChoices = false;
        AllowEnterToClose = true;
    }

    internal override ConsoleControl ContentFactory(ConsolePanel contentContainer)
    {
        ConsolePanel content = new ConsolePanel();
        content.Width = ConsoleApp.Current.LayoutRoot.Width / 2;
        content.Height = ConsoleApp.Current.LayoutRoot.Height / 2;

        Label messageLabel = content.Add(new Label() { Text = Message, X = 2, Y = 2 });
        var TextBox = TextBoxFactory?.Invoke() ?? new TextBox() { Foreground = ConsoleColor.Black, Background = ConsoleColor.White };
        content.Add(TextBox).CenterHorizontally();
        TextBox.Y = 4;

        content.SynchronizeForLifetime(nameof(content.Bounds), () => { TextBox.Width = Math.Max(0, content.Width - 4); }, content);

        TextBox.SubscribeForLifetime(nameof(TextBox.Value), () => Value = TextBox.Value, TextBox);

        TextBox.AddedToVisualTree.SubscribeOnce(() => TextBox.Application.InvokeNextCycle(() => TextBox.Focus()));
        return content;
    }
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
    public static async Task<bool> Show(Func<ConsoleControl> contentFactory, AnimatedDialogOptions options = null)
    {
        var cancelled = false;
        options = options ?? new AnimatedDialogOptions();
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

    /// <summary>
    /// Shows a dialog that contains a list of choices and a custom content area
    /// </summary>
    /// <param name="options">options you can set</param>
    /// <returns>the choice the user selected or null if the dialog was cancelled</returns>
    public static async Task<DialogChoice?> ShowChoices(DialogWithChoicesOptions options)
    {
        options.UserChoices = options.UserChoices ?? Enumerable.Empty<DialogChoice>();

        DialogChoice choice = null;

        var factory = () =>
        {
            var layout = new GridLayout("1r;2p;", "100%") { Background = options.BackgroundColor, Width = options.DialogWidth, Height = options.DialogHeight };
            var contentContainer = layout.Add(new ScrollablePanel(), 0, 0);
            var buttonContainer = layout.Add(new ConsolePanel(), 0, 1);
            var content = contentContainer.ScrollableContent.Add(options.ContentFactory(contentContainer));

            content.SynchronizeForLifetime(nameof(content.Bounds), () =>
            {
                contentContainer.ScrollableContent.Height = Math.Max(contentContainer.Height, content.Height);
                contentContainer.ScrollableContent.Width = Math.Max(contentContainer.Width, content.Width);
            }, content);

            var buttonStack = buttonContainer.Add(new StackPanel() { Height = 1, AutoSize = StackPanel.AutoSizeMode.Width, Orientation = Orientation.Horizontal, Margin = 2 }).DockToRight(padding: 2).DockToBottom(padding: 1);
            foreach (var option in options.UserChoices)
            {
                var myOption = option;
                var button = buttonStack.Add(new Button() { Text = option.DisplayText, Tag = option.Value });
                button.Pressed.SubscribeForLifetime(()=>
                {
                    choice = myOption;
                    layout.Dispose();
                }, layout);
            }
            if (options.AutoFocusChoices)
            {
                buttonStack.Children.LastOrDefault()?.Ready.SubscribeOnce(() => buttonStack.Children.Last().Focus());
            }

            options.MaxLifetime?.OnDisposed(() => layout.TryDispose());
            return layout;
        };

        var cancelled = await Show(factory, options);
        if(cancelled == false && choice == null)
        {
            choice = DialogChoice.Close.First();
        }
        return cancelled ? null : choice;
    }

    public static async Task<bool> ShowYesNoConfirmation(string message) =>
        await ShowYesNoConfirmation(message.ToString());

    public static async Task<bool> ShowYesNoConfirmation(ConsoleString message)
    {
        var options = new ShowMessageOptions(message);
        options.UserChoices = DialogChoice.YesNo;
        var ret = await ShowMessage(options);
        return ret?.Id.Equals("Yes", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static Task<DialogChoice> ShowMessage(string message) => ShowMessage(message.ToConsoleString());

    public static Task<DialogChoice> ShowMessage(ConsoleString message) =>
        ShowMessage(new ShowMessageOptions(message) { AllowEnterToClose = true, AllowEscapeToClose = true });

    public static Task<DialogChoice> ShowMessage(ShowMessageOptions options)
    {
        return ShowChoices(options);
    }

    public static async Task<ConsoleString?> ShowTextInput(ConsoleString message, ShowTextInputOptions options = null)
    {
        options = options ?? new ShowTextInputOptions() { AllowEnterToClose = true };
        options.Message = message;
        var result = await ShowChoices(options);
        var ignore = result == null || "cancel".Equals(result.Id, StringComparison.OrdinalIgnoreCase);

        return ignore ? null : options.Value;
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
