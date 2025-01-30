namespace klooie.Gaming;

public sealed class GameEvent
{
    public string Id { get; set; }
    public object? Args { get; set; }
}

internal sealed class EventBroadcaster
{
    private Dictionary<string, Event<GameEvent>> events = new Dictionary<string, Event<GameEvent>>();

    public void Subscribe(string expressionText, Action<GameEvent> handler, ILifetime lt)
    {
        var expression = BooleanExpressionParser.Parse(expressionText);
        foreach(var variable in expression.VariableNames)
        {
            if(events.TryGetValue(variable, out Event<GameEvent> matchingEvent) == false)
            {
                matchingEvent = new Event<GameEvent>();
                events.Add(variable, matchingEvent);
            }
            matchingEvent.Subscribe(handler, lt);
        }
    }

    public void SubscribeOnce(string expressionText, Action<GameEvent> handler)
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        Subscribe(expressionText, ev =>
        {
            handler(ev);
            lt.Dispose();
        }, lt);
    }

    public void Publish(string eventName, object? args)
    {
        if (eventName == null) return;
        if (events.TryGetValue(eventName, out Event<GameEvent> toFire) == false) return;
        toFire.Fire(new GameEvent() { Id = eventName, Args = args });
    }
}

