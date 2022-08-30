using PowerArgs;
namespace klooie.tests;

public static class AppTest
{
    public static void RunHeadless(string testId, Func<UITestManager, Task> test) => Run(testId, UITestMode.Headless, test);
    
    public static void Run(string testId, UITestMode mode, Func<UITestManager, Task> test) => RunCustomSize(testId, mode, 80, 50, test);

    public static void RunCustomSize(string testId, UITestMode mode, int appWidth, int appHeight, Func<UITestManager, Task> test)
    {
        ConsoleProvider.Current = new KlooieTestConsole()
        {
            BufferWidth = appWidth,
            WindowWidth = appWidth,
            WindowHeight = appHeight + 1
        };
        var app = new ConsoleApp();
        var testManager = new UITestManager(app, testId, mode);
        app.Invoke(() => test.Invoke(testManager));
        try
        {
            app.Run();
            Console.WriteLine("app finished");
        }
        finally
        {
            testManager.Finish();
        }
    }
}