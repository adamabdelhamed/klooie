using klooie.Gaming;
using System;
using System.Threading.Tasks;

namespace klooie.tests;

public static class GamingTest
{
    public static void Run(IRule theOnlyRule, string testId, UITestMode mode, Func<UITestManager, Task> test = null) =>
        Run(new ArrayRulesProvider(new IRule[] { theOnlyRule }), testId, mode, test);

    public static void Run(IRuleProvider rules, string testId, UITestMode mode, Func<UITestManager,Task> test = null)
    {
        var game = new TestGame(rules);
        var testManager = new UITestManager(game, testId, mode);
        game.Invoke(() => test?.Invoke(testManager));
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