using PowerArgs;
namespace klooie;

public abstract class DialogWithChoicesOptions : DialogOptions
{
    public int DialogWidth { get; set; } = ConsoleApp.Current != null ? ConsoleMath.Round(ConsoleApp.Current.Width * .5f) : 50;
    public int DialogHeight { get; set; } = 8;
    public bool AutoFocusChoices { get; set; } = true;
    public IEnumerable<DialogChoice> UserChoices { get; set; } = Enumerable.Empty<DialogChoice>();
    public RGB BackgroundColor { get; set; } = ConsoleString.DefaultBackgroundColor;
    public ILifetimeManager? MaxLifetime { get; set; }
    public abstract ConsoleControl ContentFactory(ConsolePanel contentContainer);
}

public static class ChoiceDialog
{
    /// <summary>
    /// Shows a dialog that contains a list of choices and a custom content area
    /// </summary>
    /// <param name="options">options you can set</param>
    /// <returns>the choice the user selected or null if the dialog was cancelled</returns>
    public static async Task<DialogChoice?> Show(DialogWithChoicesOptions options)
    {
        options.UserChoices = options.UserChoices ?? Enumerable.Empty<DialogChoice>();

        if(options.UserChoices.None() && options.AllowEnterToClose)
        {
            options.UserChoices = DialogChoice.Close;
        }

        if(options.AllowEscapeToClose && options.UserChoices.Where(c => c.Shortcut.Key == ConsoleKey.Escape).Any())
        {
            throw new ArgumentException($"You cannot use the Escape key as a dialog choice shortcut if you also set the {nameof(options.AllowEscapeToClose)} option to true");
        }

        DialogChoice choice = null;

        var factory = () =>
        {
            var showButtonContainer = options.UserChoices.Any();
            var rowSpec = showButtonContainer ? "1r;2p" : "100%";
            var layout = new GridLayout(rowSpec, "100%") { Background = options.BackgroundColor, Width = options.DialogWidth, Height = options.DialogHeight };
            var contentContainer = layout.Add(new ScrollablePanel(), 0, 0);
           
            var content = contentContainer.ScrollableContent.Add(options.ContentFactory(contentContainer));

            content.SynchronizeForLifetime(nameof(content.Bounds), () =>
            {
                contentContainer.ScrollableContent.Height = Math.Max(contentContainer.Height, content.Height);
                contentContainer.ScrollableContent.Width = Math.Max(contentContainer.Width, content.Width);
            }, content);

            if (showButtonContainer)
            {
                var buttonContainer = layout.Add(new ConsolePanel(), 0, 1);
                var buttonStack = buttonContainer.Add(new StackPanel() { Height = 1, AutoSize = StackPanel.AutoSizeMode.Width, Orientation = Orientation.Horizontal, Margin = 2 }).DockToRight(padding: 2).DockToBottom(padding: 1);
                foreach (var option in options.UserChoices)
                {
                    var myOption = option;
                    var button = buttonStack.Add(new Button() { Text = option.DisplayText, Tag = option.Value, Shortcut = option.Shortcut });
                    button.Pressed.SubscribeForLifetime(() =>
                    {
                        choice = myOption;
                        layout.Dispose();
                    }, layout);

                    // This ensures that the global enter handler on dialogs will still reflect
                    // the most recently focused button
                    button.Focused.SubscribeForLifetime(() => choice = myOption , layout);
                }
                if (options.AutoFocusChoices)
                {
                    buttonStack.Children.LastOrDefault()?.Ready.SubscribeOnce(() => buttonStack.Children.Last().Focus());
                }
            }

            options.MaxLifetime?.OnDisposed(() => layout.TryDispose());
            return layout;
        };

        if(await Dialog.Show(factory, options))
        {
            return null; // cancelled
        }



        return choice ?? DialogChoice.Close.First();
    }
}


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
    /// A keyboard shortcut for this option
    /// </summary>
    public KeyboardShortcut Shortcut { get; set; }

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
        new DialogChoice(){ DisplayText = "OK".ToConsoleString(), Id = "OK", Value = "OK", Shortcut = new KeyboardShortcut(ConsoleKey.Enter) },
        new DialogChoice(){ DisplayText = "Cancel".ToConsoleString(), Id = "Cancel", Value = "Cancel", Shortcut = new KeyboardShortcut(ConsoleKey.Escape) },
    };

    public static IEnumerable<DialogChoice> YesNo => new DialogChoice[]
    {
        new DialogChoice(){ DisplayText = "Yes".ToConsoleString(), Id = "Yes", Value = "Yes", Shortcut = new KeyboardShortcut(ConsoleKey.Enter) },
        new DialogChoice(){ DisplayText = "No".ToConsoleString(), Id = "No", Value = "No" },
    };

    public static IEnumerable<DialogChoice> Close => new DialogChoice[]
    {
        new DialogChoice(){ DisplayText = "Close".ToConsoleString(), Id = "Close", Value = "Close",Shortcut = new KeyboardShortcut(ConsoleKey.Enter) },
    };
}