using PowerArgs;

namespace klooie.Gaming;

/// <summary>
/// The base class for a game built with klooie. These games are event driven.
/// </summary>
public abstract class Game : ConsoleApp
{
    private EventBroadcaster eventBroadcaster = new EventBroadcaster();
    private RuleManager ruleManager;

    /// <summary>
    /// Gets the rules
    /// </summary>
    public IEnumerable<IRule> Rules => ruleManager.Rules;

    /// <summary>
    /// Gets the rule provider used to drive the game
    /// </summary>
    protected abstract IRuleProvider RuleProvider { get; }

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

    /// <summary>
    /// Adds a rule after startup
    /// </summary>
    /// <param name="rule">the rule to add</param>
    public void AddDynamicRule(IRule rule) => ruleManager.AddDynamicDirective(rule);

    /// <summary>
    /// Executes all of the event driven rules
    /// </summary>
    /// <returns>an async task</returns>
    protected override async Task Startup()
    {
        this.ruleManager = new RuleManager(RuleProvider);
        await ruleManager.Startup();
    }
}

