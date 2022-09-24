namespace klooie.Gaming.Code;
public class StartThreadDirective : EventDrivenDirective
{
    public string Locals { get; set; }

    [ArgRequired]
    public string FunctionName { get; set; }

    public bool FireAndForget { get; set; }

    public override async Task OnEventFired(object args)
    {
        var function = Process.Current.AST.Functions
            .Where(f => f.CanExecute && f.Name == FunctionName)
            .SingleOrDefault();

        if (function == null && FireAndForget)
        {
            return;
        }
        else if (function == null)
        {
            throw new ArgException("No function called " + FunctionName);
        }

        await function.Execute().AsTask();
    }
}
