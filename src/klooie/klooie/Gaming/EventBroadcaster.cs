using PowerArgs;

namespace klooie.Gaming;

public class GameEvent
{
    public string Id { get; set; }
    public object? Args { get; set; }
}

internal class EventBroadcaster
{
    private Dictionary<string, Event<GameEvent>> events = new Dictionary<string, Event<GameEvent>>();

    public void Subscribe(string expressionText, Action<GameEvent> handler, ILifetimeManager lt)
    {
        var expression = BooleanExpressionParser.Parse(expressionText);
        foreach(var variable in expression.VariableNames)
        {
            if(events.TryGetValue(variable, out Event<GameEvent> matchingEvent) == false)
            {
                matchingEvent = new Event<GameEvent>();
                events.Add(variable, matchingEvent);
            }
            matchingEvent.SubscribeForLifetime(handler, lt);
        }
    }

    public void Publish(string eventName, object? args)
    {
        if (events.TryGetValue(eventName, out Event<GameEvent> toFire) == false) return;
        toFire.Fire(new GameEvent() { Id = eventName, Args = args });
    }
}

