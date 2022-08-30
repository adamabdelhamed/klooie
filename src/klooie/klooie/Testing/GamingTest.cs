using klooie.Gaming;
using PowerArgs;

namespace klooie.tests;

public static class GamingTest
{
    public static void Run(IRule theOnlyRule, string testId, UITestMode mode) =>
        RunCustomSize(new ArrayRulesProvider(new IRule[] { theOnlyRule }), testId,80,50, mode, null);

    public static void RunCustomSize(IRuleProvider rules, string testId, int width, int height, UITestMode mode, Func<UITestManager,Task> test = null)
    {
        ConsoleProvider.Current = new KlooieTestConsole()
        {
            BufferWidth = width,
            WindowWidth = width,
            WindowHeight = height + 1
        };

        var game = new TestGame(rules);
        var testManager = new UITestManager(game, testId, mode);
        if (test != null)
        {
            game.Invoke(() => test?.Invoke(testManager));
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

    public TestGame() { }
    public TestGame(IRule[] rules) => this.provider = new ArrayRulesProvider(rules);
    public TestGame(IRuleProvider ruleProvider) => this.provider = ruleProvider;
}