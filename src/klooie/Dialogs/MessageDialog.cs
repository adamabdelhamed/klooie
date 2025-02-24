namespace klooie;

/// <summary>
/// Options for showing a message in a dialog
/// </summary>
public class ShowMessageOptions : DialogWithChoicesOptions
{
    /// <summary>
    /// Gets the message
    /// </summary>
    public ConsoleString Message { get; private set; }

    /// <summary>
    /// Creates options given a message to show
    /// </summary>
    /// <param name="msg">a message to show</param>
    public ShowMessageOptions(ConsoleString msg)
    {
        Message = msg;
    }

    /// <summary>
    /// Creates options given a message to show
    /// </summary>
    /// <param name="msg">a message to show</param>
    public ShowMessageOptions(string msg) => Message = msg.ToConsoleString();

    /// <summary>
    /// Creates a label that shows the message
    /// </summary>
    /// <param name="contentContainer"></param>
    /// <returns></returns>
    public override ConsoleControl ContentFactory(ConsolePanel contentContainer) => 
        new TextViewer() { Y = 1, X = 2, Width = contentContainer.Width - 4, Text = Message };
}

/// <summary>
/// A dialog helper that shows messages
/// </summary>
public static class MessageDialog
{
    /// <summary>
    /// Shows the given message
    /// </summary>
    /// <param name="message">the message shown</param>
    public static Task Show(string message) => Show(message.ToConsoleString());

    /// <summary>
    /// Shows the given message
    /// </summary>
    /// <param name="message">the message shown</param>
    public static Task Show(ConsoleString message) => Show(new ShowMessageOptions(message));

    /// <summary>
    /// Shows the given message and lets the user choose from yes and no
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>true if yes was chosen, false otherwise</returns>
    public static async Task<bool> ShowYesOrNo(ConsoleString message)
    {
        var options = new ShowMessageOptions(message);
        options.UserChoices = DialogChoice.YesNo;
        var ret = await Show(options);
        return ret?.Id.Equals("Yes", StringComparison.OrdinalIgnoreCase) == true;
    }
    /// <summary>
    /// Shows the given message and lets the user choose from yes and no
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>true if yes was chosen, false otherwise</returns>
    public static Task<bool> ShowYesOrNo(string message) => ShowYesOrNo(message.ToConsoleString());

    /// <summary>
    /// Shows a dialog using the provided options
    /// </summary>
    /// <param name="options">the options</param>
    public static Task<DialogChoice?> Show(ShowMessageOptions options) => ChoiceDialog.Show(options);
}