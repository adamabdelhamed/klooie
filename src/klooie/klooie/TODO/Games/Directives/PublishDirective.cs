namespace klooie.Gaming.Code;
public class PublishDirective : EventDrivenDirective
{
    [ArgRequired]
    [ArgPosition(0)]
    public string EventId { get; set; }

    public DynamicArg EventArgs { get; set; }
    public DynamicArg If { get; set; }

    [ArgCantBeCombinedWith(nameof(If))]
    public DynamicArg IfNot { get; set; }

    public override Task OnEventFired(object args)
    {
        var shouldFire = If == null && IfNot == null ? true :
                         If != null ? Evaluator.EvaluateBooleanExpression(If.StringValue) :
                         !Evaluator.EvaluateBooleanExpression(IfNot.StringValue); 
        if (shouldFire)
        {
            var effectiveArgs = EventArgs != null ? EventArgs.ObjectValue : args;
            Game.Current.Publish(EventId, effectiveArgs);
        }
        return Task.CompletedTask;
    }
}
