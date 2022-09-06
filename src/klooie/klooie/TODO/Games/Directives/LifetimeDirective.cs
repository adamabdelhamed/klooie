using PowerArgs;

namespace klooie.Gaming;
public class CrossLevelLifetime : Lifetime { }

public abstract class LifetimeDirective : EventDrivenDirective
{
    public static CrossLevelLifetime Get(string id) => IdToLifetimesLifetimes[id];
    protected static Dictionary<string, CrossLevelLifetime> IdToLifetimesLifetimes { get; private set; } = new Dictionary<string, CrossLevelLifetime>();

    public static void EndAll()
    {
        foreach (var lt in IdToLifetimesLifetimes.Values.ToArray())
        {
            lt.Dispose();
        }
    }
}

public class NewLifetimeDirective : LifetimeDirective
{
    [ArgRequired]
    public string Id { get; set; }
    public override Task OnEventFired(object args)
    {
        var lt = new CrossLevelLifetime();
        IdToLifetimesLifetimes.Add(Id, lt);
        lt.OnDisposed(() => IdToLifetimesLifetimes.Remove(Id));
        return Task.CompletedTask;
    }
}

public class EndLifetimeDirective : LifetimeDirective
{
    [ArgRequired]
    public string Id { get; set; }

    public override Task OnEventFired(object args)
    {
        if (IdToLifetimesLifetimes.ContainsKey(Id))
        {
            IdToLifetimesLifetimes[Id].TryDispose();
        }
        return Task.CompletedTask;
    }
}
