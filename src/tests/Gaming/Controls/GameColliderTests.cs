using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class GameColliderTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup() => TestContextHelper.GlobalSetup();

    [TestMethod]
    public void GameCollider_Init() => GamingTest.Run(new GamingTestOptions()
    {
        Test = async (context) =>
        {
            var c = new GameCollider();
            Assert.AreEqual(Game.Current.MainColliderGroup, c.ColliderGroup);
            Game.Current.Stop();
        },
        Mode = UITestMode.Headless,
    });
}