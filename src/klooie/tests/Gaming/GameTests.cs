
using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class GameTests
{

    [TestMethod]
    public void EventBroadcaster_SingleVariable()
    {
        var game = new Game();
        game.Invoke(() =>
        {
            var receiveCount = 0;
            using (var subLt = new Lifetime())
            {
                game.Subscribe("Ready", (e) =>
                {
                    Assert.AreEqual("Ready", e.Id);
                    receiveCount++;
                }, subLt);

                Assert.AreEqual(0, receiveCount);
                game.Publish("Ready");
                Assert.AreEqual(1, receiveCount);
            }

            // lifetime is over, subscription should be terminated
            game.Publish("Ready");
            Assert.AreEqual(1, receiveCount);
            game.Stop();
        });
        game.Run();
    }

    [TestMethod]
    public void EventBroadcaster_Expression()
    {
        var game = new Game();
        game.Invoke(() =>
        {
            var receiveCount = 0;
            game.Subscribe("Ready|Not", (e) =>
            {
                Assert.IsTrue(e.Id == "Ready" || e.Id == "Not");
                receiveCount++;
            }, game);

            Assert.AreEqual(0, receiveCount);
            game.Publish("SomeOtherEvent");
            Assert.AreEqual(0, receiveCount);

            game.Publish("Ready");
            Assert.AreEqual(1, receiveCount);

            game.Publish("Not");
            Assert.AreEqual(2, receiveCount);

            game.Publish("SomeOtherEvent");
            Assert.AreEqual(2, receiveCount);

            game.Stop();
        });
        game.Run();
    }
}

