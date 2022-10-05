namespace klooie.Gaming.Code;
public abstract class FunctionDirective : Directive
{
    [ArgRequired]
    public string Name { get; set; }

    [ArgIgnore]
    public Function Function { get; private set; }

    public override async Task ExecuteAsync()
    {
            Function = AST.Functions
             .Where(f => f.Tokens.Where(t => t.Value == Name).Count() == 1)
             .SingleOrDefault();

        if (Function == null) throw new Exception("Function not found: "+Name);
         await OnFunctionIdentified(Function);
        
    }

    protected abstract Task OnFunctionIdentified(Function myFunction);
}

public abstract class EventDrivenFunctionDirective : EventDrivenDirective
{
    [ArgRequired]
    public string Name { get; set; }

    [ArgIgnore]
    public Function Function { get; private set; }

    public override async Task OnEventFired(object args)
    {
        Function = AST.Functions
            .Where(f => f.Tokens.Where(t => t.Value == Name).Count() == 1)
            .SingleOrDefault();
        
        if (Function == null) throw new Exception("Function not found: " + Name);

        await OnFunctionIdentified(Function);
    }

    protected abstract Task OnFunctionIdentified(Function myFunction);
}