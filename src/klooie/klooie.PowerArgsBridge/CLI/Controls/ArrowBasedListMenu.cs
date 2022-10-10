namespace PowerArgs.CLI.Controls;
public class ArrowBasedListMenu<T> : ProtectedConsolePanel where T : class
{
    private List<T> menuItems;
    private Func<T, ConsoleString> formatter;
    private Func<T, bool> isEnabled;

    public int SelectedIndex { get => Get<int>(); set => Set(value); }
    public ConsoleKey? AlternateUp { get; set; }
    public ConsoleKey? AlternateDown { get; set; }

    public T? SelectedItem => menuItems.Count > 0 ? menuItems[SelectedIndex] : null;
    public Event<T> ItemActivated { get; private init; } = new Event<T>();

    public ArrowBasedListMenu(List<T> menuItems) : this(menuItems, item => true, item => ("" + item).ToConsoleString()) { }

    public ArrowBasedListMenu(List<T> menuItems, Func<T, bool> isEnabled, Func<T, ConsoleString> formatter)
    {
        this.menuItems = menuItems;
        this.isEnabled = isEnabled;
        this.formatter = formatter;
        GuardAgainstNullArguments();
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

    private void GuardAgainstNullArguments()
    {
        if (menuItems == null) throw new ArgumentNullException(nameof(menuItems));
        if (isEnabled == null) throw new ArgumentNullException(nameof(isEnabled));
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
    }
}
