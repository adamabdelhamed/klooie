using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class TextColliderTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        ConsoleProvider.Current = new KlooieTestConsole()
        {
            BufferWidth = 80,
            WindowWidth = 80,
            WindowHeight = 51
        };
    }

    [TestMethod]
    public void TextCollider_Display() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext.TestId(),
        Test = async (context) =>
        {
            var c = Game.Current.GamePanel.Add(new TextCollider("TEXT".ToRed()));
            await context.PaintAndRecordKeyFrameAsync();
            Game.Current.Stop();
        },
        Mode = UITestMode.KeyFramesVerified,
    });
}