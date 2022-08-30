using PowerArgs;

namespace klooie.Gaming;

/// <summary>
/// The base class for a game built with klooie. These games are event driven.
/// </summary>
public abstract class Game : ConsoleApp, IDelayProvider
{
    private EventBroadcaster eventBroadcaster;
    private RuleManager ruleManager;
    private PauseManager pauseManager;
    private ColliderGroup mainColliderGroup;

    /// <summary>
    /// returns true if the game is currently paused
    /// </summary>
    public bool IsPaused => pauseManager.State == PauseManager.PauseState.Paused;

    /// <summary>
    /// Pauses the game
    /// </summary>
    protected void Pause() => pauseManager.State = PauseManager.PauseState.Paused;

    /// <summary>
    /// Pauses the game
    /// </summary>
    protected void Resume() => pauseManager.State = PauseManager.PauseState.Running;

    /// <summary>
    /// Gets the rules
    /// </summary>
    public IEnumerable<IRule> Rules => ruleManager.Rules;

    /// <summary>
    /// Gets the rule provider used to drive the game
    /// </summary>
    protected abstract IRuleProvider RuleProvider { get; }

    /// <summary>
    /// Gets access to the primary, pause-aware collider group
    /// </summary>
    public ColliderGroup MainColliderGroup => mainColliderGroup;

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

    public Game()
    {
        this.eventBroadcaster = new EventBroadcaster();
        this.pauseManager = new PauseManager();
    }

    /// <summary>
    /// Executes all of the event driven rules
    /// </summary>
    /// <returns>an async task</returns>
    protected override async Task Startup()
    {
        this.mainColliderGroup = new ColliderGroup(this) { PauseManager = this.pauseManager };
        this.ruleManager = new RuleManager(RuleProvider);
        await ruleManager.Startup();
    }

    public Task DelayAsync(double ms) => pauseManager.DelayProvider.DelayAsync(ms);

    public Task DelayAsync(TimeSpan timeout) => pauseManager.DelayProvider.DelayAsync(timeout);

    public Task DelayAsync(Event ev, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) =>
        pauseManager.DelayProvider.DelayAsync(ev, timeout, evalFrequency);

    public Task DelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) =>
        pauseManager.DelayProvider.DelayAsync(condition, timeout, evalFrequency);

    public Task<bool> TryDelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) =>
        pauseManager.DelayProvider.TryDelayAsync(condition, timeout, evalFrequency);

    public Task DelayFuzzyAsync(float ms, double maxDeltaPercentage = 0.1) =>
        pauseManager.DelayProvider.DelayFuzzyAsync(ms, maxDeltaPercentage);
}

