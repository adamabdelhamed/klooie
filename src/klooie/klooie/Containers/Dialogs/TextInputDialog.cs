using PowerArgs;
namespace klooie;

/// <summary>
/// Options for a dialog that shows a text box
/// </summary>
public class ShowTextInputOptions : ShowMessageOptions
{
    /// <summary>
    /// Set this to use a custom text box, otherwise a default will be created
    /// </summary>
    public Func<TextBox>? TextBoxFactory { get; set; }

    /// <summary>
    /// Gets the value that the user entered in the text box
    /// </summary>
    public ConsoleString? Value { get; private set; }

    /// <summary>
    /// Creates the options given a message
    /// </summary>
    /// <param name="msg">the message to show</param>
    public ShowTextInputOptions(ConsoleString msg) : base(msg)
    {
        AutoFocusChoices = false;
    }

    /// <summary>
    /// Creates a label and a text box
    /// </summary>
    /// <param name="contentContainer">the container</param>
    /// <returns>a label and a text box</returns>
    public override ConsoleControl ContentFactory(ConsolePanel contentContainer)
    {
        ConsolePanel content = new ConsolePanel();
        content.SubscribeOnce(nameof(content.Parent), () => content.Fill());

        Label messageLabel = content.Add(new Label() { Text = Message, X = 2, Y = 1 });
        var TextBox = TextBoxFactory?.Invoke() ?? new TextBox() { Foreground = ConsoleColor.Black, Background = ConsoleColor.White };
        content.Add(TextBox).CenterHorizontally();
        TextBox.Y = 4;

        content.SynchronizeForLifetime(nameof(content.Bounds), () => { TextBox.Width = Math.Max(0, content.Width - 4); }, content);
        TextBox.Subscribe(nameof(TextBox.Value), () => Value = TextBox.Value, TextBox);

        TextBox.AddedToVisualTree.SubscribeOnce(() => TextBox.Application.InvokeNextCycle(() => TextBox.Focus()));
        return content;
    }
}

/// <summary>
/// A dialog helper for getting a single text input from the user
/// </summary>
public static class TextInputDialog
{
    /// <summary>
    /// Shows the dialog given options
    /// </summary>
    /// <param name="options">the dialog options</param>
    /// <returns>the text entered or null if the dialog was cancelled</returns>
    public static async Task<ConsoleString?> Show(ShowTextInputOptions options)
    {
        var result = await ChoiceDialog.Show(options);
        var ignore = result == null || "cancel".Equals(result.Id, StringComparison.OrdinalIgnoreCase);

        return ignore ? null : options.Value;
    }

    /// <summary>
    /// Shows the dialog given a message
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>the text entered or null if the dialog was cancelled</returns>
    public static Task<ConsoleString?> Show(string message) =>
        Show(new ShowTextInputOptions(message.ToConsoleString()));

    /// <summary>
    /// Shows the dialog given a message
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>the text entered or null if the dialog was cancelled</returns>
    public static Task<ConsoleString?> Show(ConsoleString message) =>
        Show(new ShowTextInputOptions(message));
}