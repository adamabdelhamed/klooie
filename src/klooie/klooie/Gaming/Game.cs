namespace klooie.Gaming;

/// <summary>
/// The base class for a game built with klooie. These games are event driven.
/// </summary>
public abstract class Game : ConsoleApp, IDelayProvider
{
    /// <summary>
    /// The id for the Ready event that fires after all initial rules are executed
    /// </summary>
    public const string ReadyEventId = "Ready";

    private EventBroadcaster eventBroadcaster;
    private RuleManager ruleManager;
    private PauseManager pauseManager;
    private ColliderGroup mainColliderGroup;

    /// <summary>
    /// Gets a reference to the current game
    /// </summary>
    public static Game Current => ConsoleApp.Current as Game;

    /// <summary>
    /// returns true if the game is currently paused
    /// </summary>
    public bool IsPaused => pauseManager.State == PauseManager.PauseState.Paused;

    /// <summary>
    /// Gets the time that has been elapsed in the MainColliderGroup
    /// </summary>
    public static TimeSpan Now => Current != null ? Current.MainColliderGroup.Now : TimeSpan.Zero;

    /// <summary>
    /// Pauses the game
    /// </summary>
    public void Pause() => pauseManager.State = PauseManager.PauseState.Paused;

    /// <summary>
    /// Pauses the game
    /// </summary>
    public void Resume() => pauseManager.State = PauseManager.PauseState.Running;

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
    /// The main panel where game controls will be placed
    /// </summary>
    public virtual ConsolePanel GamePanel => LayoutRoot;

    /// <summary>
    /// Variables that can be referenced by rules
    /// </summary>
    public ObservableObject RuleVariables { get; protected set; }

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
    /// Creates a new Game
    /// </summary>
    public Game()
    {
        this.eventBroadcaster = new EventBroadcaster();
        this.pauseManager = new PauseManager();
        RuleVariables = new ObservableObject();
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
        Publish(ReadyEventId);
    }

    /// <summary>
    /// implements a pause aware delay action
    /// </summary>
    /// <param name="ms">the amount of time in ms to delay</param>
    /// <returns>a task</returns>
    public Task Delay(double ms) => pauseManager.DelayProvider.Delay(ms);

    /// <summary>
    /// implements a pause aware delay action
    /// </summary>
    /// <param name="timeout">the amount of time to delay</param>
    /// <returns>a task</returns>
    public Task Delay(TimeSpan timeout) => pauseManager.DelayProvider.Delay(timeout);
}

