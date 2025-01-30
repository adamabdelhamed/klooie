using System.Runtime.CompilerServices;
using System.Linq;
namespace klooie.Gaming;

/// <summary>
/// The base class for a game built with klooie. These games are event driven.
/// </summary>
public class Game : ConsoleApp, IDelayProvider
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
    /// During startup, this will be populated with the current rule that's running.
    /// This is useful if you need to detect rules that have gotten stuck
    /// </summary>
    public IRule CurrentStartupRule => ruleManager?.Current;

    /// <summary>
    /// Gets a reference to the current game
    /// </summary>
    public static Game Current => ConsoleApp.Current as Game;

    /// <summary>
    /// an event that fires when the game is paused
    /// </summary>
    public Event<ILifetime> Paused => pauseManager.OnPaused;

    /// <summary>
    /// returns true if the game is currently paused
    /// </summary>
    public bool IsPaused => pauseManager.IsPaused;

    /// <summary>
    /// Gets the time that has been elapsed in the MainColliderGroup
    /// </summary>
    public static TimeSpan Now => Current != null ? Current.MainColliderGroup.Now : TimeSpan.Zero;

    /// <summary>
    /// Pauses the game
    /// </summary>
    public virtual void Pause() => pauseManager.IsPaused = true;

    /// <summary>
    /// Pauses the game
    /// </summary>
    public virtual void Resume() => pauseManager.IsPaused = false;

    /// <summary>
    /// Gets the rules
    /// </summary>
    public IEnumerable<IRule> Rules => ruleManager.Rules;

    /// <summary>
    /// Gets the rule provider used to drive the game
    /// </summary>
    protected virtual IRuleProvider RuleProvider => ArrayRulesProvider.Empty;

    /// <summary>
    /// Gets access to the primary, pause-aware collider group
    /// </summary>
    public ColliderGroup MainColliderGroup => mainColliderGroup;

    /// <summary>
    /// The main panel where game controls will be placed
    /// </summary>
    public virtual ConsolePanel GamePanel => LayoutRoot;

    /// <summary>
    /// Gets the main gaming area, which by default is the size of LayoutRoot, but
    /// can be replaced with a camera.
    /// </summary>
    public virtual RectF GameBounds => LayoutRoot.Bounds;

    /// <summary>
    /// Variables that can be referenced by rules
    /// </summary>
    public SpecialReverseDictionary RuleVariables { get; protected set; }

    /// <summary>
    /// Subscribes to a game event for some amount of time. You can use boolean expressions using and ('&'), or ('|') and
    /// grouping with parentheses. If an event is published that mathces then your handler will be called.
    /// </summary>
    /// <param name="expressionText">the subscription expression</param>
    /// <param name="handler">the handler to call when a matching event is published</param>
    /// <param name="lt">the duration of the subscription</param>
    public void Subscribe(string expressionText, Action<GameEvent> handler, ILifetime lt) =>
        eventBroadcaster.Subscribe(expressionText, handler, lt);

    /// <summary>
    /// Subscribes to a game event for up to one occurrance. You can use boolean expressions using and ('&'), or ('|') and
    /// grouping with parentheses. If an event is published that mathces then your handler will be called.
    /// </summary>
    /// <param name="expressionText">the subscription expression</param>
    /// <param name="handler">the handler to call when a matching event is published</param>
    public void SubscribeOnce(string expressionText, Action<GameEvent> handler) =>
        eventBroadcaster.SubscribeOnce(expressionText, handler);

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
    public void AddDynamicRule(IRule rule) => Invoke(async()=> await ruleManager.AddDynamicDirective(rule));

    /// <summary>
    /// Creates a new Game
    /// </summary>
    public Game()
    {
        this.eventBroadcaster = new EventBroadcaster();
        this.pauseManager = new PauseManager();
        RuleVariables = new SpecialReverseDictionary();
    }

    /// <summary>
    /// Executes all of the event driven rules
    /// </summary>
    /// <returns>an async task</returns>
    protected override async Task Startup()
    {
        PaintEnabled = false;
        this.mainColliderGroup = new ColliderGroup(this) { PauseManager = this.pauseManager };
        this.ruleManager = new RuleManager(RuleProvider);
        await ruleManager.Startup();
        Publish(ReadyEventId);
        PaintEnabled = true;
        await RequestPaintAsync();
    }

    /// <summary>
    /// implements a pause aware delay action
    /// </summary>
    /// <param name="ms">the amount of time in ms to delay</param>
    /// <returns>a task</returns>
    public Task Delay(double ms) => pauseManager.Delay(ms);

    /// <summary>
    /// implements a pause aware delay action
    /// </summary>
    /// <param name="timeout">the amount of time to delay</param>
    /// <returns>a task</returns>
    public Task Delay(TimeSpan timeout) => pauseManager.Delay(timeout);
}

public class SpecialReverseDictionary
{
    private Dictionary<string, (object Value, Event Changed)> dict = new Dictionary<string, (object Value, Event Changed)>();


    public void Subscribe(string key, Action handler, ILifetime lifetime)
    {
        if (dict.ContainsKey(key) == false)
        {
            dict.Add(key, (null, new Event()));
        }

        dict[key].Changed.Subscribe(handler, lifetime);
    }

    public void Sync(string key, Action handler, ILifetime lifetime)
    {
        Subscribe(key, handler, lifetime);
        handler();
    }

    public void Set<T>(T value, [CallerMemberName]string key = null)
    {
        if (dict.ContainsKey(key))
        {
            dict[key] = (value, dict[key].Changed);
            dict[key].Changed.Fire();
        }
        else
        {
            dict.Add(key, (value, new Event()));
        }
    }

    public T Get<T>([CallerMemberName]string key = null) => dict.ContainsKey(key) ?  (T)(dict[key].Value) : default;

    public bool TryGetValue<T>(string key, out T value)
    {
        if (dict.TryGetValue(key, out var val))
        {
            value = (T)val.Value;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public bool ContainsKey(string key) => dict.ContainsKey(key);

    public Dictionary<string,object> ToDictionary() => dict.ToDictionary(d2 => d2.Key, d2 => d2.Value.Value);
}