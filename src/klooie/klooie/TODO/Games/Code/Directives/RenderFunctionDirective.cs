namespace klooie.Gaming.Code;
public class RenderFunctionDirective : EventDrivenDirective
{
    [ArgRequired]
    public string Name { get; set; }
    public DynamicArg Left { get; set; }
    public DynamicArg Top { get; set; }

    public bool LineNumbers { get; set; }

    public override Task OnEventFired(object args)
    {
        var function = Process.Current.AST.Functions
            .Where(f => f.Name == Name)
            .Single();
        Process.Current.RenderCode(function, LineNumbers, Left.FloatValue, Top.FloatValue);
        return Task.CompletedTask;
    }
}
