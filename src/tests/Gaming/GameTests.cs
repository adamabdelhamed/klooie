
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
    public TestContext TestContext { get; set; }

    public class TestRule : IRule
    {
        private Func<Task> body;
        public TestRule(Func<Task> body) => this.body = body;
        public Task ExecuteAsync() => body();
    }

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
    public void EventBroadcaster_SingleVariable()
    {
        var game = new TestGame();
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
    public void EventBroadcaster_SingleVariableOnce()
    {
        var game = new TestGame();
        game.Invoke(() =>
        {
            var receiveCount = 0;
            game.SubscribeOnce("Ready", (e) =>
            {
                Assert.AreEqual("Ready", e.Id);
                receiveCount++;
            });

            Assert.AreEqual(0, receiveCount);
            game.Publish("Ready");
            Assert.AreEqual(1, receiveCount);
            

            // lifetime is over, subscription should be terminated
            game.Publish("Ready");
            Assert.AreEqual(1, receiveCount);
            game.Stop();
        });
        game.Run();
    }

    [TestMethod]
    public void EventBroadcaster_Expression() => GamingTest.Run(FuncRule.Create(async() =>
    { 
        var receiveCount = 0;
        Game.Current.Subscribe("Ready|Not", (e) =>
        {
            Assert.IsTrue(e.Id == "Ready" || e.Id == "Not");
            receiveCount++;
        }, Game.Current);

        Assert.AreEqual(0, receiveCount);
        Game.Current.Publish("SomeOtherEvent");
        Assert.AreEqual(0, receiveCount);

        Game.Current.Publish("Ready");
        Assert.AreEqual(1, receiveCount);

        Game.Current.Publish("Not");
        Assert.AreEqual(2, receiveCount);

        Game.Current.Publish("SomeOtherEvent");
        Assert.AreEqual(2, receiveCount);

        Game.Current.Stop();
    }),TestContext.TestId(), UITestMode.Headless);

    [TestMethod]
    public void Rules_Basic()
    {
        int count = 0;
        int readyCount = 0;
        GamingTest.RunCustomSize(new ArrayRulesProvider(new IRule[]
        {
            new TestRule(async() => count++),
            new TestRule(async() => count++),
            new TestRule(async() => Game.Current.Subscribe(Game.ReadyEventId,e=> readyCount++,Game.Current)),
        }), TestContext.TestId(), 1, 1, UITestMode.Headless);

        Assert.AreEqual(2, count);
        Assert.AreEqual(1, readyCount);
    }

    [TestMethod]
    public void Rules_Exceptions()
    {
        try
        {
            GamingTest.RunCustomSize(new ArrayRulesProvider(new IRule[]
            {
                new TestRule(async() => throw new Exception("threw")),
            }), TestContext.TestId(), 1, 1, UITestMode.Headless);
            Assert.Fail("An exception should have been thrown");
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex.Message == "threw");
        }
    }

    [TestMethod]
    public void Rules_Dynamic()
    {
        int count = 0;
        GamingTest.RunCustomSize(new ArrayRulesProvider(new IRule[]
        {
            new TestRule(async() => count++),
            new TestRule(async() => count++),
        }), TestContext.TestId(), 1, 1, UITestMode.Headless,async(context)=>
        {
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, Game.Current.Rules.Count());
            Game.Current.AddDynamicRule(new TestRule(async () => count++));
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, Game.Current.Rules.Count());
            Game.Current.Stop();
        });
    }

    [TestMethod]
    [TestCategory(Categories.ConsoleApp)]
    public void Pause_Basic() => GamingTest.Run(FuncRule.Create(async()=>
    {
        var period = 500;
        var expected = period *.5f;
        var minExpected = period * .48f;
        var maxExpected = period * .58f;
        var gameTask = Game.Current.Delay(period);
        var wallClockTask = Task.Delay(period);

        Assert.IsFalse(Game.Current.IsPaused);
        await Task.Delay(period/2);
        Game.Current.Pause();
        Assert.IsTrue(Game.Current.IsPaused);
        await wallClockTask;
        Assert.IsTrue(Game.Current.IsPaused);
        var now = Game.Now;
        Console.WriteLine($"Pause expected between {minExpected} and {maxExpected}, actual: {Math.Round(now.TotalMilliseconds)}");
        Assert.IsTrue(Game.Now.TotalMilliseconds >= minExpected);
        Assert.IsTrue(Game.Now.TotalMilliseconds <= maxExpected);
        await Task.Delay(100);
        Assert.IsTrue(Game.Current.IsPaused);
        Assert.AreEqual(now,Game.Now);
        Game.Current.Resume();
        Assert.IsFalse(Game.Current.IsPaused);
        await Task.Delay(100);
        Assert.IsFalse(Game.Current.IsPaused);
        Assert.AreNotEqual(now,Game.Now);
        Game.Current.Stop();

    }), TestContext.TestId(), UITestMode.Headless);


    [TestMethod]
    [TestCategory(Categories.ConsoleApp)]
    public void Pause_Idempotent() => GamingTest.Run(FuncRule.Create(async()=>
    {
        var period = 500;
        var expected = period *.5f;
        var minExpected = period * .48f;
        var maxExpected = period * .58f;
        var gameTask = Game.Current.Delay(period);
        var wallClockTask = Task.Delay(period);

        await Task.Delay(period/2);
        Game.Current.Pause();
        Game.Current.Pause();
        Game.Current.Pause();
        await wallClockTask;
        var now = Game.Now;
        Console.WriteLine($"Pause expected between {minExpected} and {maxExpected}, actual: {Math.Round(now.TotalMilliseconds)}");
        Assert.IsTrue(Game.Now.TotalMilliseconds >= minExpected);
        Assert.IsTrue(Game.Now.TotalMilliseconds <= maxExpected);
        await Task.Delay(100);
        Assert.AreEqual(now,Game.Now);
        Game.Current.Resume();
        Game.Current.Resume();
        Game.Current.Resume();
        await Task.Delay(100);
        Assert.AreNotEqual(now,Game.Now);
        Game.Current.Stop();
    }), TestContext.TestId(), UITestMode.Headless);
}

