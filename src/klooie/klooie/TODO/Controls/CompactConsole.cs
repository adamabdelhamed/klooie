using PowerArgs;
namespace klooie;
public class RecommendedAction : Attribute, ICommandLineActionMetadata { }

public abstract class CompactConsole : ConsolePanel
{
    public bool IsAssistanceEnabled { get; set; } = true;
    public TextBox InputBox { get; private set; }
    private CommandLineArgumentsDefinition def;
    private TextViewer outputLabel;
    private Lifetime focusLt;
    public ConsoleString WelcomeMessage { get; set; } = "Welcome to the console".ToWhite();
    public ConsoleString EscapeMessage { get; set; } = "Press escape to resume".ToGray();

    public bool HideWelcomePanel { get; set; }

    public bool SuperCompact { get; set; }

    public CompactConsole()
    {
        Subscribe(nameof(Bounds), () => HardRefresh(), this);
        this.Ready.SubscribeOnce(async () =>
        {
            await Task.Yield();
            HardRefresh();
        });
    }

    protected abstract CommandLineArgumentsDefinition CreateDefinition();
    protected virtual bool HasHistory() { return false; }
    protected virtual void AddHistory(string history) { }
    protected virtual ConsoleString GetHistoryPrevious() => throw new NotImplementedException();
    protected virtual ConsoleString GetHistoryNext() => throw new NotImplementedException();

    protected virtual void OnInputBoxReady() { }
    protected virtual async Task Run(ArgAction toRun)
    {
        await toRun.InvokeAsync();
        SetOutput(null);
    }

    Lifetime refreshLt = new Lifetime();
    public void HardRefresh(ConsoleString outputValue = null)
    {
        var wasInputBlocked = this.InputBox?.IsInputBlocked == true;
        var wasFocusable = this.InputBox?.CanFocus == true;
        refreshLt?.Dispose();
        refreshLt = new Lifetime();
        var myLt = refreshLt;
        Controls.Clear();

        var minHeight = SuperCompact ? 1 : 5;

        if (Width < 10 || Height < minHeight) return;

        def = CreateDefinition();

        var options = new GridLayoutOptions()
        {
            Columns = new System.Collections.Generic.List<GridColumnDefinition>()
                {
                    new GridColumnDefinition(){ Type = GridValueType.Pixels, Width = 2 },           // 0 empty
                    new GridColumnDefinition(){ Type = GridValueType.RemainderValue, Width = 1 },   // 1 content
                    new GridColumnDefinition(){ Type = GridValueType.Pixels, Width = 2 },           // 2 empty
                },
            Rows = new System.Collections.Generic.List<GridRowDefinition>()
                {
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 0 empty
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 1 welcome message
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 2 press escape message
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 3 empty
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 4 input
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 5 empty
                    new GridRowDefinition(){ Type = GridValueType.RemainderValue, Height = 1, },// 6 output
                    new GridRowDefinition(){ Type = GridValueType.Pixels, Height = 1, },        // 7 empty
                }
        };

        if (SuperCompact)
        {
            options.Rows.RemoveAt(0); // empty
            options.Rows.RemoveAt(0); // welcome
            options.Rows.RemoveAt(0); // press escape
            options.Rows.RemoveAt(0); // empty
            options.Rows.RemoveAt(options.Rows.Count - 1); // empty
            options.Rows.RemoveAt(options.Rows.Count - 1); // output
            options.Rows.RemoveAt(options.Rows.Count - 1); // empty
        }

        var gridLayout = Add(new GridLayout(options.GetRowSpec(), options.GetColumnSpec()));



        gridLayout.Fill();

        var top = SuperCompact ? 0 : 1;

        if (SuperCompact == false)
        {
            var welcomePanel = gridLayout.Add(new ConsolePanel() { IsVisible = !HideWelcomePanel }, 1, top++);
            welcomePanel.Add(new Label() { Text = WelcomeMessage }).CenterHorizontally();

            var escapePanel = gridLayout.Add(new ConsolePanel() { IsVisible = !HideWelcomePanel }, 1, top++);
            escapePanel.Add(new Label() { Text = EscapeMessage }).CenterHorizontally();
            
            top++;
        }

        var inputPanel = gridLayout.Add(new ConsolePanel() { }, 1, top++);
        inputPanel.Add(new Label() { Text = "CMD> ".ToConsoleString() });
        InputBox = inputPanel.Add(new TextBox() { CanFocus = wasFocusable, IsInputBlocked = wasInputBlocked, X = "CMD> ".Length, Width = inputPanel.Width - "CMD> ".Length, Foreground = RGB.Gray, Background = RGB.Black });
        InputBox.Editor.TabHandler.TabCompletionHandlers.Add(new PowerArgsRichCommandLineReader(def, new List<ConsoleString>(), false));
        OnInputBoxReady();
        top++;
        if (myLt == refreshLt)
        {
            InputBox.Focused.Subscribe(() =>
            {
                if (focusLt != null && focusLt.IsExpired == false && focusLt.IsExpiring == false)
                {
                    focusLt.Dispose();
                }

                focusLt = new Lifetime();


                Application.PushKeyForLifetime(ConsoleKey.Tab, async () =>
                {
                    await OnHandleHey(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
                }, focusLt);
                Application.PushKeyForLifetime(ConsoleKey.Tab, ConsoleModifiers.Shift, async () =>
                {
                    await OnHandleHey(new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false));
                }, focusLt);

            }, refreshLt);

            InputBox.Unfocused.Subscribe(() =>
            {
                if (focusLt != null && focusLt.IsExpired == false && focusLt.IsExpiring == false)
                {
                    focusLt.Dispose();
                }
            }, refreshLt);

            if (InputBox.CanFocus)
            {
                InputBox.Focus();
            }
        }

        if (SuperCompact == false)
        {
            var outputPanel = gridLayout.Add(new ConsolePanel() { Background = RGB.Black }, 1, top);
            var text = string.IsNullOrWhiteSpace(outputValue?.StringValue) == false ? outputValue :
                string.IsNullOrWhiteSpace(outputLabel?.Text?.StringValue) == false ? outputLabel?.Text :
                CreateAssistiveText();
            outputLabel = outputPanel.Add(new TextViewer(text)).Fill();
        }
        InputBox.KeyInputReceived.Subscribe(async (keyInfo) => await OnHandleHey(keyInfo), InputBox);
    }

    private async Task OnHandleHey(ConsoleKeyInfo keyInfo)
    {
        if (InputBox.IsInputBlocked) return;
        if (keyInfo.Key == ConsoleKey.Enter && string.IsNullOrWhiteSpace(InputBox.Value?.ToString())) return;
        OnKeyPress(keyInfo);
        if (keyInfo.Key == ConsoleKey.Enter)
        {
            ConsoleString output = ConsoleString.Empty;
            try
            {
                var args = Args.Convert(InputBox.Value.ToString());
                AddHistory(InputBox.Value.ToString());

                if (def.ExceptionBehavior?.Policy == ArgExceptionPolicy.StandardExceptionHandling)
                {
                    def.ExceptionBehavior = new ArgExceptionBehavior(ArgExceptionPolicy.DontHandleExceptions);
                }

                ArgAction action;
                ConsoleOutInterceptor.Instance.Attach();
                try
                {
                    action = Args.ParseAction(def, args);
                }
                finally
                {
                    ConsoleOutInterceptor.Instance.Detatch();
                }
                InputBox.Dispose();
                output = new ConsoleString(ConsoleOutInterceptor.Instance.ReadAndClear());

                if (action.Cancelled == false)
                {
                    var oldDef = Args.GetAmbientDefinition();
                    try
                    {
                        Args.SetAmbientDefinition(def);
                        await Run(action);
                    }
                    finally
                    {
                        Args.SetAmbientDefinition(oldDef);
                    }
                }
            }
            catch (Exception ex)
            {

                var inner = ex;
                if (ex is AggregateException && (ex as AggregateException).InnerExceptions.Count == 1)
                {
                    inner = ex.InnerException;
                }

                if (ex is ArgException == false)
                {
                    throw;
                }

                output = inner.Message.ToRed();
            }
            finally
            {
                if (IsExpired == false)
                {
                    HardRefresh(output);
                }
            }
        }
        else if (keyInfo.Key == ConsoleKey.Tab)
        {
            ConsoleCharacter? prototype = InputBox.Value.Length == 0 ? (ConsoleCharacter?)null : InputBox.Value[InputBox.Value.Length - 1];
            InputBox.Editor.RegisterKeyPress(keyInfo, prototype);
            InputBox.Value = InputBox.Editor.CurrentValue;
        }
        else if (keyInfo.Key == ConsoleKey.UpArrow)
        {
            if (HasHistory())
            {
                InputBox.Value = GetHistoryPrevious();
                SetOutput(CreateAssistiveText());
            }
        }
        else if (keyInfo.Key == ConsoleKey.DownArrow)
        {
            if (HasHistory())
            {
                InputBox.Value = GetHistoryNext();
                SetOutput(CreateAssistiveText());
            }
        }
        else if (RichTextCommandLineReader.IsWriteable(keyInfo))
        {
            SetOutput(CreateAssistiveText());
        }
        AfterKeyPress(keyInfo);
    }

    protected virtual void OnKeyPress(ConsoleKeyInfo info) { }
    protected virtual void AfterKeyPress(ConsoleKeyInfo info) { }

    private void SetOutput(ConsoleString text)
    {
        if (outputLabel != null)
        {
            outputLabel.Text = text;
        }
    }

    public void Write(ConsoleString text)
    {
        if (outputLabel != null)
        {
            outputLabel.Text += text;
        }
    }
    public void WriteLine(ConsoleString text) => Write(text + "\n");
    public void Clear()
    {
        if (outputLabel != null)
        {
            outputLabel.Text = ConsoleString.Empty;
        }
    }

    protected virtual ConsoleString Parse(string content) => ConsoleString.Parse(content);

    public ConsoleString CreateAssistiveText()
    {
        if (IsAssistanceEnabled == false)
        {
            return ConsoleString.Empty;
        }

        List<CommandLineAction> candidates = def.Actions.Where(a => a.Metadata.WhereAs<OmitFromUsageDocs>().None()).ToList();
        if (InputBox.Value != null && InputBox.Value.Length > 0)
        {
            var command = InputBox.Value.Split(" ".ToConsoleString()).FirstOrDefault();
            command = command ?? ConsoleString.Empty;
            candidates = candidates.Where(a => a.DefaultAlias.StartsWith(command.StringValue, StringComparison.OrdinalIgnoreCase)).ToList();

            if (candidates.Count == 0)
            {
                return $"\nNo commands start with {InputBox.Value.ToString()}".ToRed();
            }
        }

        if (candidates.Where(c => c.Metadata.HasMeta<RecommendedAction>()).None())
        {
            return CreateTable(candidates, true);
        }
        else
        {
            var recommended = candidates.Where(c => c.Metadata.HasMeta<RecommendedAction>()).ToList();
            var other = candidates.Where(c => !c.Metadata.HasMeta<RecommendedAction>()).ToList();

            var recommendedHeader = recommended.None() ? ConsoleString.Empty : " recommended commands ".ToConsoleString(RGB.Black, RGB.Yellow) + "\n\n".ToConsoleString();
            var recommendedTable = recommended.None() ? ConsoleString.Empty : CreateTable(recommended, true);
            var otherHeader = other.None() ? ConsoleString.Empty : "\n\nother commands\n".ToGray();
            var otherTable = other.None() ? ConsoleString.Empty : CreateTable(other, false).ToGray();

            return recommendedHeader + recommendedTable + otherHeader + otherTable;
        }
    }

    private ConsoleString CreateTable(List<CommandLineAction> candidates, bool includeHeaders)
    {
        var builder = new ConsoleTableBuilder();
        var headers = includeHeaders ? new List<ConsoleString>()
            {
                "command".ToYellow(),
                "description".ToYellow(),
                "example".ToYellow(),
            } : new List<ConsoleString>() { ConsoleString.Empty, ConsoleString.Empty, ConsoleString.Empty };

        var rows = new List<List<ConsoleString>>();

        foreach (var candidate in candidates)
        {
            var row = new List<ConsoleString>();
            rows.Add(row);
            row.Add(candidate.DefaultAlias.ToLower().ToCyan());
            row.Add(Parse(candidate.Description));
            row.Add(candidate.HasExamples == false ? ConsoleString.Empty : candidate.Examples.First().Example.ToGreen());



            if (candidates.Count == 1)
            {
                foreach (var arg in candidate.Arguments.Where(a => a.Metadata.Where(m => m is OmitFromUsageDocs).None()))
                {
                    var argDescription = !arg.HasDefaultValue ? ConsoleString.Empty : Parse($"[DarkYellow]\\[Default: [Yellow]{arg.DefaultValue}[DarkYellow]] ");
                    argDescription += string.IsNullOrEmpty(arg.Description) ? ConsoleString.Empty : Parse(arg.Description);
                    argDescription += !arg.IsEnum ? ConsoleString.Empty : "values: ".ToYellow() + string.Join(", ", arg.EnumValuesAndDescriptions).ToYellow();

                    row = new List<ConsoleString>();
                    rows.Add(row);
                    row.Add(" -".ToWhite() + arg.DefaultAlias.ToLower().ToWhite() + (arg.IsRequired ? "*".ToRed() : ConsoleString.Empty));
                    row.Add(argDescription);
                    row.Add(ConsoleString.Empty);

                }
            }
        }
        return builder.FormatAsTable(headers, rows);
    }
}
