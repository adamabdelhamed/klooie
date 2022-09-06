namespace klooie.Gaming.Code;
public class StartThreadDirective : EventDrivenDirective
{
    public string Locals { get; set; }

    [ArgRequired]
    public string EntryPoint { get; set; }

    public bool FireAndForget { get; set; }

    public override async Task OnEventFired(object args)
    {
        var waitStart = Game.Current.MainColliderGroup.Now;
        while (Game.Current.MainColliderGroup.Now - waitStart < TimeSpan.FromSeconds(1))
        {
            await Task.Yield();
        }

        var function = Process.Current.AST.Functions
            .Where(f => f.CanExecute && f.Name == EntryPoint)
            .SingleOrDefault();

        if (function == null && FireAndForget)
        {
            return;
        }
        else if (function == null)
        {
            throw new ArgException("No function called " + EntryPoint);
        }

        await function.Execute().AsTask();
    }
}
