using PowerArgs;
namespace klooie;

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

    public override ConsoleControl ContentFactory(ConsolePanel contentContainer)
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

public static class TextInputDialog
{
    public static async Task<ConsoleString?> Show(ConsoleString message, ShowTextInputOptions options = null)
    {
        options = options ?? new ShowTextInputOptions() { AllowEnterToClose = true };
        options.Message = message;
        var result = await ChoiceDialog.Show(options);
        var ignore = result == null || "cancel".Equals(result.Id, StringComparison.OrdinalIgnoreCase);

        return ignore ? null : options.Value;
    }
}