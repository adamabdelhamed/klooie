namespace klooie.Gaming.Code;

public class ShowCodeDirective : EventDrivenDirective
{
    [ArgRequired]
    public DynamicArg Left { get; set; }
    [ArgRequired]
    public DynamicArg Top { get; set; }

    public override Task OnEventFired(object args)
    {
        Process.Current.RenderCode(Process.Current.AST.Root, Left.FloatValue, Top.FloatValue);
        return Task.CompletedTask;
    }
}

