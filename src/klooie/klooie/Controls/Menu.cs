namespace klooie;
/// <summary>
/// A panel that lists a set of menu items and lets the user navigate them using the up
/// and down arrows
/// </summary>
/// <typeparam name="T"></typeparam>
public class Menu<T> : ProtectedConsolePanel where T : class
{
    private List<T> menuItems;
    private Func<T, ConsoleString> formatter;
    private Func<T, bool> isEnabled;

    /// <summary>
    /// Gets or sets the selected index
    /// </summary>
    public int SelectedIndex { get => Get<int>(); set => Set(value); }

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
    public T? SelectedItem => menuItems.Count > 0 ? menuItems[SelectedIndex] : null;

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
    }

    private void AddMenuItems()
    {
        var stack = ProtectedPanel.Add(new StackPanel() { Orientation = Orientation.Vertical, Margin = 1 }).Fill();
        menuItems.ForEach(menuItem => stack.Add(new Label() { Tag = menuItem }).FillHorizontally());
    }

    private void SetupEventHandlers()
    {
        this.CanFocus = true;
        this.Focused.Sync(RefreshLabels, this);
        this.Unfocused.Subscribe(RefreshLabels, this);
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
            FirePropertyChanged(nameof(SelectedItem));
            RefreshLabels();
        }
        else if (wasDownPressed && canAdvanceForwards)
        {
            SelectedIndex++;
            FirePropertyChanged(nameof(SelectedItem));
            RefreshLabels();
        }
        else if (wasEnterPressed && canActivateItem)
        {
            ItemActivated.Fire(SelectedItem);
        }
    }

    private ConsoleString SelectedItemFormatter(T item)=> (formatter(item).StringValue).ToConsoleString(HasFocus ? RGB.Black : Foreground, HasFocus ? RGB.Cyan : Background);
    
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
        Subscribe(nameof(SelectedIndex), () =>
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
