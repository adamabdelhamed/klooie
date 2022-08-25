using PowerArgs;
namespace klooie;

public class ShowMessageOptions : DialogWithChoicesOptions
{
    public ConsoleString Message { get; private set; }

    public ShowMessageOptions(ConsoleString msg)
    {
        AllowEnterToClose = true;
        Message = msg;
    }

    public ShowMessageOptions(string msg) => Message = msg.ToConsoleString();

    public override ConsoleControl ContentFactory(ConsolePanel contentContainer)
    {
        var ret = new Label(LabelRenderMode.MultiLineSmartWrap) { Y = 1, X = 2, Width = contentContainer.Width - 4, Text = Message };
        return ret;
    }
}

public static class MessageDialog
{
    public static Task<DialogChoice> Show(string message) => Show(message.ToConsoleString());

    public static Task<DialogChoice> Show(ConsoleString message) =>
        Show(new ShowMessageOptions(message) { AllowEnterToClose = true, AllowEscapeToClose = true });

    public static Task<DialogChoice> Show(ShowMessageOptions options) => ChoiceDialog.Show(options);
    

    public static Task<bool> ShowYesNo(string message) => ShowYesNo(message.ToString());

    public static async Task<bool> ShowYesNo(ConsoleString message)
    {
        var options = new ShowMessageOptions(message);
        options.UserChoices = DialogChoice.YesNo;
        var ret = await Show(options);
        return ret?.Id.Equals("Yes", StringComparison.OrdinalIgnoreCase) == true;
    }
}