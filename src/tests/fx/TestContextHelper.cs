using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
namespace klooie.tests;

public static class TestContextHelper
{
    public static string TestId(this TestContext context) => context == null ? "Unknown" : $"{context.FullyQualifiedTestClassName}.{context.TestName}";

    public static void GlobalSetup()
    {
        ConsoleProvider.Current = new KlooieTestConsole() { BufferWidth = 80, WindowWidth = 80, WindowHeight = 50 };
    }
}
