using PowerArgs;

namespace klooie.Gaming;

/// <summary>
/// The base class for a game built with klooie. These games are event driven.
/// </summary>
public class Game : ConsoleApp
{
    private EventBroadcaster eventBroadcaster = new EventBroadcaster();

    /// <summary>
    /// Subscribes to a game event for some amount of time. You can use boolean expressions using and ('&'), or ('|') and
    /// grouping with parentheses. If an event is published that mathces then your handler will be called.
    /// </summary>
    /// <param name="expressionText">the subscription expression</param>
    /// <param name="handler">the handler to call when a matching event is published</param>
    /// <param name="lt">the duration of the subscription</param>
    public void Subscribe(string expressionText, Action<GameEvent> handler, ILifetimeManager lt) =>
        eventBroadcaster.Subscribe(expressionText, handler, lt);

    /// <summary>
    /// Publishes a game event with optional args.
    /// </summary>
    /// <param name="eventName">the event being published</param>
    /// <param name="args">the data that goes along with the event</param>
    public void Publish(string eventName, object args = null) =>
        eventBroadcaster.Publish(eventName, args);
}

