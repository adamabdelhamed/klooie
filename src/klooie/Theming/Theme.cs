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

    public virtual void Apply(ConsolePanel root = null, ILifetimeManager lt = null)
    {
        tracker = ThemeEvaluator.Apply(Styles, root, lt);
        (lt ?? root)?.OnDisposed(() => tracker = null);
    }
    public virtual Task Apply(BuiltInEpicThemeTransitionKind kind, ConsolePanel root = null, ILifetimeManager lt = null) => Apply(new BuiltInEpicThemeTransition(kind), root, lt);
    public virtual Task Apply(EpicThemeTransition effect, ConsolePanel root = null, ILifetimeManager lt = null) => effect.Apply(this, root, lt);

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

