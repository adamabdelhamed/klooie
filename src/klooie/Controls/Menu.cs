namespace klooie;
/// <summary>
/// A panel that lists a set of menu items and lets the user navigate them using the up
/// and down arrows
/// </summary>
/// <typeparam name="T"></typeparam>
public partial class Menu<T> : ProtectedConsolePanel where T : class
{
    private List<T> menuItems;
    private Func<T, ConsoleString> formatter;
    private Func<T, bool> isEnabled;

    /// <summary>
    /// Gets or sets the selected index
    /// </summary>
    public partial int SelectedIndex { get; set; }

    /// <summary>
    /// Optionally define an alternate key for up to be honored in addition to the up arrow
    /// </summary>
    public ConsoleKey? AlternateUp { get; set; }

    /// <summary>
    /// Optionally define an alternate key for down to be honored in addition to the down arrow
    /// </summary>
    public ConsoleKey? AlternateDown { get; set; }

    /// <summary>
    /// Gets the currently selected item
    /// </summary>
    public partial T? SelectedItem { get; private set; }

    /// <summary>
    /// An event that fires when the user activates the selected item
    /// </summary>
    public Event<T> ItemActivated { get; private init; } = new Event<T>();

    /// <summary>
    /// Creates a menu given a set of menu items. When this constructor is used the
    /// menu will call ToString() on the item to create its display string.
    /// </summary>
    /// <param name="menuItems">the menu items</param>
    public Menu(List<T> menuItems) : this(menuItems, item => true, item => ("" + item).ToConsoleString()) { }

    /// <summary>
    /// Creates a menu given a set of menu items. This constructor lets you define functions
    /// that can be used to disable and format items
    /// </summary>
    /// <param name="menuItems">the items to display</param>
    /// <param name="isEnabled">a function that tells the control if an item is enabled</param>
    /// <param name="formatter">a function that formats an item into a console string</param>
    public Menu(List<T> menuItems, Func<T, bool> isEnabled, Func<T, ConsoleString> formatter)
    {
        this.menuItems = menuItems;
        this.isEnabled = isEnabled;
        this.formatter = formatter;
        GuardAgainstNullArguments();
        GuardAgainstInvalidSelectedIndexSetter();
        AddMenuItems();
        SetupEventHandlers();
        SubscribeToAnyPropertyChange(this, RefreshLabels, this);
    }

    private void AddMenuItems()
    {
        var stack = ProtectedPanel.Add(new StackPanel() { Orientation = Orientation.Vertical, Margin = 1 }).Fill();
        SyncBackground(stack);
        menuItems.ForEach(menuItem => stack.Add(new Label() { Tag = menuItem }).FillHorizontally());
        SyncBackground(stack.Children.ToArray());
    }

    private void SetupEventHandlers()
    {
        this.CanFocus = true;
        this.Focused.Sync(RefreshLabels, this);
        this.Unfocused.Subscribe(RefreshLabels, this);
        SelectedIndexChanged.Sync(() => SelectedItem = menuItems[SelectedIndex], this);
        SelectedIndexChanged.Subscribe(RefreshLabels, this);
        this.KeyInputReceived.Subscribe(OnKeyPress, this);
    }

    private void OnKeyPress(ConsoleKeyInfo obj)
    {
        var wasUpPressed = obj.Key == ConsoleKey.UpArrow || (AlternateUp.HasValue && obj.Key == AlternateUp.Value);
        var wasDownPressed = obj.Key == ConsoleKey.DownArrow || (AlternateDown.HasValue && obj.Key == AlternateDown.Value);
        var wasEnterPressed = obj.Key == ConsoleKey.Enter;
        var canAdvanceBackwards = SelectedIndex > 0 && isEnabled(menuItems[SelectedIndex - 1]);
        var canAdvanceForwards = SelectedIndex < menuItems.Count - 1 && isEnabled(menuItems[SelectedIndex + 1]);
        var canActivateItem = SelectedItem != null;

        if (wasUpPressed && canAdvanceBackwards)
        {
            SelectedIndex--;
            RefreshLabels();
        }
        else if (wasDownPressed && canAdvanceForwards)
        {
            SelectedIndex++;
            RefreshLabels();
        }
        else if (wasEnterPressed && canActivateItem)
        {
            ItemActivated.Fire(SelectedItem);
        }
    }

    private ConsoleString SelectedItemFormatter(T item)
    {
        var ret = (formatter(item).StringValue).ToConsoleString(HasFocus ? Background : Foreground, HasFocus ? FocusColor : Background);
        return ret;
    }
    private static void RefreshLabels(object me)
    {
        var _this = (Menu<T>)me;
        _this.RefreshLabels();
    }

    private void RefreshLabels()
    {
        foreach (var label in ProtectedPanel.Descendents.WhereAs<Label>().Where(l => l.Tag is T))
        {
            var item = (T)label.Tag;
            var isSelected = ReferenceEquals(label.Tag, SelectedItem);
            label.Text = isSelected ? SelectedItemFormatter(item) : formatter(item);
        }
    }

    private void GuardAgainstInvalidSelectedIndexSetter()
    {
        SelectedIndexChanged.Subscribe(() =>
        {
            if (SelectedIndex < 0 || SelectedIndex >= menuItems.Count)
            {
                throw new ArgumentException($"{nameof(SelectedIndex)} '{SelectedIndex}'is out of range");
            }
        }, this);
    }

    private void GuardAgainstNullArguments()
    {
        if (menuItems == null) throw new ArgumentNullException(nameof(menuItems));
        if (isEnabled == null) throw new ArgumentNullException(nameof(isEnabled));
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
    }
}
