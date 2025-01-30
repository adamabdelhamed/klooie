namespace klooie.Theming;

/// <summary>
/// A base for all themes that can be used to style ConsoleApps in a way that makes it
/// easy to have more than one look and feel option for your app.
/// </summary>
public abstract class Theme
{
    private ThemeApplicationTracker tracker;

    /// <summary>
    /// Gets the styles that will be applied
    /// </summary>
    public abstract Style[] Styles { get; }

    /// <summary>
    /// Applies this theme to the current console app
    /// </summary>
    /// <param name="root">the panel to theme, defaults to the app's layout root</param>
    /// <param name="lt">how long should this theme be applied, defaults to the current root's lifetime</param>

    public virtual void Apply(ConsolePanel root = null, ILifetime lt = null)
    {
        tracker = ThemeEvaluator.Apply(Styles, root, lt);
        (lt ?? root)?.OnDisposed(() => tracker = null);
    }
    public virtual Task Apply(BuiltInEpicThemeTransitionKind kind, ConsolePanel root = null, ILifetime lt = null) => Apply(new BuiltInEpicThemeTransition(kind), root, lt);
    public virtual Task Apply(EpicThemeTransition effect, ConsolePanel root = null, ILifetime lt = null) => effect.Apply(this, root, lt);

    /// <summary>
    /// Gets all styles that have never been applied
    /// </summary>
    /// <returns>an enumerable of styles that have never been applied</returns>
    public IEnumerable<Style> WhereNeverApplied() => tracker?.WhereNeverApplied() ?? Styles;

    /// <summary>
    /// Creates a theme given a set of styles.
    /// </summary>
    /// <param name="styles">a set of styles</param>
    /// <returns>a theme for the given styles</returns>
    public static Theme FromStyles(Style[] styles) => new SecretTheme(styles);

    /// <summary>
    /// Creates a theme given a set of styles.
    /// </summary>
    /// <param name="styles">a set of styles</param>
    /// <returns>a theme for the given styles</returns>
    public static Theme FromStyles(StyleBuilder builder) => new SecretTheme(builder.ToArray());

    private class SecretTheme : Theme
    {
        private Style[] styles;
        public override Style[] Styles => styles;
        public SecretTheme(Style[] styles) => this.styles = styles;
    }
}

/// <summary>
/// A theme that can be chained to another theme, enabling complex
/// themes to be built up from simpler ones.
/// </summary>
/// <typeparam name="T">The host theme type that defines global styles for the app</typeparam>
public abstract class ChainedTheme<T> where T : Theme
{
    /// <summary>
    /// Gets the theme that this theme is chained to
    /// </summary>
    protected T Theme { get; private set; }

    /// <summary>
    /// Creates a chained theme given a host theme
    /// </summary>
    /// <param name="theme">the host theme</param>
    public ChainedTheme(T theme) => this.Theme = theme;

    /// <summary>
    /// Chains this theme's styles into the given builder
    /// </summary>
    /// <param name="builder">a style builder to cahin onto</param>
    /// <returns>the same builder you passed in</returns>
    public abstract StyleBuilder Chain(StyleBuilder builder);
}

