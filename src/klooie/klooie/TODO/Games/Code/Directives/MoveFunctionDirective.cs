namespace klooie.Gaming.Code;
public class MoveFunctionDirective : FunctionDirective
{
    [ArgIgnore]
    public Function Function { get; set; }

    [ArgRequired]
    public DynamicArg Left { get; set; }
    [ArgRequired]
    public DynamicArg Top { get; set; }

    public DynamicArg If { get; set; }

    protected override Task OnFunctionIdentified(Function myFunction)
    {
        Function = myFunction;
        return Task.CompletedTask;
    }
}
