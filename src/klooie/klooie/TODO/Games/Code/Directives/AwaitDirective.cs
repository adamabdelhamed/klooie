using PowerArgs;

namespace klooie.Gaming.Code;
public class AwaitDirective : Directive
{
    [ArgRequired]
    public string Target { get; set; }
    [ArgDefaultValue(1000)]
    public int Latency { get; set; }
    public bool Log { get; set; }

    [ArgRequired]
    public DynamicArg OutgoingData { get; set; }

    public DynamicArg ReturnData { get; set; }
    [ArgDefaultValue(750)]
    public float AsyncDuration { get; set; }

    public override Task ExecuteAsync()
    {
        var targetStatement = GetClosest<RunningCodeStatement>(false);
        if (targetStatement != null)
        {
            targetStatement.AsyncInfo = this;
        }
        return Task.CompletedTask;
    }
}
