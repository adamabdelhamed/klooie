using klooie.Gaming;

namespace klooie.tests;

public class GamingTestOptions
{
    public IRuleProvider Rules { get; set; } = ArrayRulesProvider.Empty;
    public string TestId { get; set; }
    public int GameWidth { get; set; } = 80;
    public int GameHeight { get; set; } = 50;
    public Func<UITestManager,Task> Test { get; set; }
    public UITestMode Mode { get; set; }
    public bool Camera { get; set; } = false;
    public Event<LocF>? FocalPointChanged { get; set; }
    public IStopwatch Stopwatch { get; set; }
}

public static class GamingTest
{
    public static UITestMode? TestModeOverride;
    public static void Run(IRule theOnlyRule, string testId, UITestMode mode) =>
        RunCustomSize(new ArrayRulesProvider(new IRule[] { theOnlyRule }), testId,80,50, mode, null);

    public static void Run(string testId, UITestMode mode, Func<UITestManager, Task> test = null) =>
        RunCustomSize(ArrayRulesProvider.Empty, testId, 80, 50, mode, test);

    public static void RunCustomSize(string testId, UITestMode mode, int w, int h, Func<UITestManager, Task> test = null) =>
        RunCustomSize(ArrayRulesProvider.Empty, testId, w, h, mode, test);

    public static void RunCustomSize(IRuleProvider rules, string testId, int width, int height, UITestMode mode, Func<UITestManager, Task> test = null) => Run(new GamingTestOptions()
    {
        Rules = rules,
        Mode = mode,
        TestId = testId,
        Test = test,
        GameWidth = width,
        GameHeight = height,
    });
    

    public static void Run(GamingTestOptions options)
    {
        if(TestModeOverride.HasValue)
        {
            options.Mode = TestModeOverride.Value;
        }

        ConsoleProvider.Current = new KlooieTestConsole()
        {
            BufferWidth = options.GameWidth,
            WindowWidth = options.GameWidth,
            WindowHeight = options.GameHeight + 1
        };
        if(options.Mode == UITestMode.HeadOnly)
        {
            ConsoleProvider.Current = new StdConsoleProvider();
        }
        var game = new TestGame(options);
        var testManager = new UITestManager(game, options.TestId, options.Mode);
        if (options.Test != null)
        {
            game.Invoke(() => options.Test?.Invoke(testManager));
        }
        else
        {
            game.Invoke(() => game.Stop());
        }
        try
        {
            game.Run();
            Console.WriteLine("Game finished");
        }
        finally
        {
            testManager.Finish();
        }
    }
}

public class TestGame : Game
{
    protected override IRuleProvider RuleProvider => provider ?? ArrayRulesProvider.Empty;
    private IRuleProvider provider;
    private Camera camera;
    private GamingTestOptions options;
    public TestGame(GamingTestOptions options)
    {
        this.options = options;
        var myRule = FuncRule.Create(async () =>
        {
            if (options.Camera)
            {
                camera = LayoutRoot.Add(new Camera()).Fill();
                LayoutRoot.Sync(nameof(LayoutRoot.Background), () =>
                {
                    camera.Background = LayoutRoot.Background;
                }, camera);

                camera.Subscribe(nameof(camera.Background), () =>
                {
                    var bg = camera.Background;
                }, camera);

                camera.BigBounds = new RectF(-500, -500, 1000, 1000);
                camera.PointAt(camera.BigBounds.Center);

                options.FocalPointChanged?.Subscribe((b) => camera.PointAt(b), this);
            }
        });

        if(options.Camera)
        {
            this.provider = options.Rules != null ? new RuleWrapper(options.Rules, new IRule[] { myRule })
                : new ArrayRulesProvider(new IRule[] { myRule });
        }
        else
        {
            this.provider = options.Rules;
        }
    }

    protected override async Task Startup()
    {
        await base.Startup();
        if (options.Mode == UITestMode.RealTimeFYI)
        {
            var fr = LayoutRoot.Add(new Label() { Foreground = RGB.White, Background = RGB.Black }).DockToRight(padding: 2).DockToTop(padding: 1);
            Invoke(async () =>
            {
                while (ShouldContinue)
                {
                    await Task.Delay(100);
                    fr.Text = (FramesPerSecond + " FPS").ToConsoleString();
                }
            });
        }
    }

    public TestGame() { }

    public override ConsolePanel GamePanel => camera ?? base.GamePanel;
    public override RectF GameBounds => camera?.BigBounds ?? base.GameBounds;
}

public class RuleWrapper : IRuleProvider
{
    private IRuleProvider wrapped;
    private IEnumerable<IRule> additions;
    public RuleWrapper(IRuleProvider provider, IEnumerable<IRule> additions)
    {
        this.wrapped = provider;
        this.additions = additions;
    }
    public IRule[] Rules => wrapped.Rules.Concat(additions).ToArray();
}