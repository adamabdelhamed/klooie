using PowerArgs;

namespace klooie.Gaming.Code;
public class EntryPointDirective : Directive
{
    public string EntryPoint { get; set; }

    [ArgDefaultValue("data")]
    public DynamicArg Display { get; set; }

    [ArgRequired]
    public new string Source { get; set; }

    [ArgDefaultValue(ConsoleColor.Green)]
    public ConsoleColor Background { get; set; }

    [ArgDefaultValue(ConsoleColor.Black)]
    public ConsoleColor Foreground { get; set; }

    [ArgIgnore]
    public Function TargetFunction { get; set; }

    [ArgIgnore]
    public CodeControl InitialDestination { get; set; }

    public override Task ExecuteAsync()
    {
        if (EntryPoint == null) return Task.CompletedTask;

        TargetFunction = Process.Current.AST.Functions
        .Where(f => f.Tokens.Where(t => t.Value == EntryPoint).Count() == 1)
        .Single();

        if (TargetFunction != null)
        {
            InitialDestination = CodeControl.CodeElements.Where(c => c.Token?.Statement == TargetFunction).OrderByDescending(c => c.Left + c.Width).First();
            var ep = ExternalEndpointElement.GetEndpointCreateIfNoExists(this);
            TargetFunction.Source = ep;
        }

        return Task.CompletedTask;
    }
}
