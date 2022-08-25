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
                    var button = buttonStack.Add(new Button() { Text = option.DisplayText, Tag = option.Value });
                    button.Pressed.SubscribeForLifetime(() =>
                    {
                        choice = myOption;
                        layout.Dispose();
                    }, layout);

                    button.Focused.SubscribeForLifetime(() =>
                    {
                        choice = myOption;
                    }, layout);
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