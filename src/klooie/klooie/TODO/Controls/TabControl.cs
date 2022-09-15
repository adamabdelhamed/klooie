
namespace klooie;

public class TabControlOptions
{
    public ObservableCollection<string> Tabs { get; private set; } = new ObservableCollection<string>();
    public Func<string,ConsoleControl> BodyFactory { get; set; }
    public bool TreatAKeyAndDKeyAsLeftRight { get; set; }
    public TabControlOptions(IEnumerable<string> tabs) => tabs.ForEach(t => Tabs.Add(t));
    public TabControlOptions(params string[] tabs) => tabs.ForEach(t => Tabs.Add(t));
}

public class TabControl : ProtectedConsolePanel
{
    private GridLayout layout;
    private ConsolePanel tabContainer;
    private StackPanel tabStack;
    private ConsolePanel body;
    private NoFrillsLabel currentTabLabel;
    private Lifetime arrowKeyLifetime;
    private List<NoFrillsLabel> tabs = new List<NoFrillsLabel>();
    private string _currentTab;

    public string CurrentTab
    {
        get => _currentTab;
        set
        {
            if (Options.Tabs.Where(t => t == value).None()) throw new ArgumentException($"{value} is not one of your tab options");
            if (value != CurrentTab)
            {
                _currentTab = value;
                body.Controls.Clear();
                body.Add(Options.BodyFactory(value)).Fill();
            }
        }
    }
    public TabControlOptions Options { get; private set; }
    public RGB FocusBGColor { get => Get<RGB>(); set => Set(value); }  
    public RGB FocusFGColor { get => Get<RGB>(); set => Set(value); }  

    public TabControl(TabControlOptions options)
    {
        this.Options = options;
        FocusBGColor = RGB.Cyan;
        FocusFGColor = RGB.Black;
        Foreground = RGB.Yellow;
        Ready.SubscribeOnce(Init);
    }

    private void Init()
    {
        layout = ProtectedPanel.Add(new GridLayout("3p;1r","100%")).Fill();
        tabContainer = layout.Add(new ConsolePanel(), 0, 0);
        body = layout.Add(new ConsolePanel(), 0, 1);
        Options.Tabs.Changed.Sync(RefreshTabs, this);
        ConsoleApp.Current.FocusChanged.Subscribe(c => RefreshFocus(), this);

        Sync(nameof(Background), () =>
        {
            layout.Background = this.Background;
            tabContainer.Background = this.Background;
            if(tabStack != null) tabStack.Background = this.Background;
        }, this);
    }

    private void RefreshFocus()
    {
        var hasFocus = tabStack.Controls.Contains(ConsoleApp.Current.FocusedControl);
        if (hasFocus)
        {
            SetupArrowKeyNavitation();
            currentTabLabel = ConsoleApp.Current.FocusedControl as NoFrillsLabel;
            CurrentTab = currentTabLabel.Text.ToString();
        }

        var currentTabFg = hasFocus ? FocusFGColor : RGB.Gray;
        var currentTabBg = hasFocus ? FocusBGColor : RGB.DarkGray;
        foreach (var label in tabStack.Controls.WhereAs<NoFrillsLabel>())
        {
            var tabString = label.Text.ToString();
            var fg = tabString == CurrentTab ? currentTabFg : Foreground;
            var bg = tabString == CurrentTab ? currentTabBg : Background;
            label.Text = tabString.ToConsoleString(fg, bg);
        }
    }

    private void RefreshTabs()
    {
        if (Options.Tabs.None()) throw new NotSupportedException("You need to add at least one tab");
        if (Options.Tabs.Distinct().Count() != Options.Tabs.Count) throw new Exception("More than one tab has the same value");

        tabs.Clear();
        tabContainer.Controls.Clear();
        tabStack = tabContainer.Add(new StackPanel() { Orientation = Orientation.Horizontal, Margin = 3, AutoSize = StackPanel.AutoSizeMode.Both, Background = this.Background }).CenterBoth();
        CurrentTab = (CurrentTab != null && Options.Tabs.Where(t => t.ToString() == CurrentTab).Any()) ? CurrentTab : Options.Tabs.First().ToString();
        currentTabLabel = null;
        foreach(var str in Options.Tabs)
        {
            var label = tabStack.Add(new NoFrillsLabel(str.ToConsoleString()) { CompositionMode = CompositionMode.BlendBackground, CanFocus = true });
            currentTabLabel = str == CurrentTab ? label : currentTabLabel;
            tabs.Add(label);
        }
        RefreshFocus();
    }

    private void SetupArrowKeyNavitation()
    {
        arrowKeyLifetime?.Dispose();
        var hasFocus = tabStack.Controls.Contains(ConsoleApp.Current.FocusedControl);
        if (hasFocus == false) return;
        arrowKeyLifetime = this.CreateChildLifetime();

        var next = () =>
        {
            if (ConsoleApp.Current.FocusedControl != tabs.Last()) ConsoleApp.Current.MoveFocus();
            else tabs.First().Focus();
        };

        var previous = () =>
        {
            if (ConsoleApp.Current.FocusedControl != tabs.First()) ConsoleApp.Current.MoveFocus(false);
            else tabs.Last().Focus();
        };

        if (Options.TreatAKeyAndDKeyAsLeftRight)
        {
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.D, next, arrowKeyLifetime);
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.A, previous, arrowKeyLifetime);
        }
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.RightArrow, next, arrowKeyLifetime);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.LeftArrow, previous, arrowKeyLifetime);
    }
}

