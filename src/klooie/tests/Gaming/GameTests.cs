
using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class GameTests
{
    public class StaticGame : Game
    {
        protected override IRuleProvider RuleProvider => provider ?? ArrayRulesProvider.Empty;
        private ArrayRulesProvider provider;

        public StaticGame() { }
        public StaticGame(IRule[] rules) => this.provider = new ArrayRulesProvider(rules);
    }

    public class TestRule : IRule
    {
        private Func<Task> body;
        public TestRule(Func<Task> body) => this.body = body;
        public Task ExecuteAsync() => body();
    }

    [TestMethod]
    public void EventBroadcaster_SingleVariable()
    {
        var game = new StaticGame();
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
        var game = new StaticGame();
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

    [TestMethod]
    public void Rules_Basic()
    {
        int count = 0;
        var game = new StaticGame(new IRule[]
        {
            new TestRule(async() => count++),
            new TestRule(async() => count++),
        });

        game.Invoke(game.Stop);
        game.Run();
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void Rules_Exceptions()
    {
        var game = new StaticGame(new IRule[]
        {
            new TestRule(async() => throw new Exception("threw")),
        });

        game.Invoke(game.Stop);
        try
        {
            game.Run();
            Assert.Fail("An exception should have been thrown");
        }
        catch(Exception ex)
        {
            Assert.IsTrue(ex.Message == "threw");
        }
    }

    [TestMethod]
    public void Rules_Dynamic()
    {
        int count = 0;
        var game = new StaticGame(new IRule[]
        {
            new TestRule(async() => count++),
            new TestRule(async() => count++),
        });

        game.Invoke(async()=>
        {
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, game.Rules.Count());
            game.AddDynamicRule(new TestRule(async () => count++));
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, game.Rules.Count());
            game.Stop();
        });
        game.Run();
    }
}

