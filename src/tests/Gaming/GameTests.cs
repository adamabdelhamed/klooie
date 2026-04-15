
using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
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
        }), TestContext.TestId(), 1, 1, UITestMode.Headless, async (context) =>
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
    public void Pause_Basic() => GamingTest.Run(FuncRule.Create(async () =>
    {
        var period = 500;
        var expected = period * .5f;
        var minExpected = period * .48f;
        var maxExpected = period * .58f;

        var wallClockTask = Task.Delay(period);

        Assert.IsFalse(Game.Current.IsPaused);
        await Task.Delay(period / 2);
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
        Assert.AreEqual(now, Game.Now);
        Game.Current.Resume();
        Assert.IsFalse(Game.Current.IsPaused);
        await Task.Delay(100);
        Assert.IsFalse(Game.Current.IsPaused);
        Assert.AreNotEqual(now, Game.Now);
        Game.Current.Stop();

    }), TestContext.TestId(), UITestMode.Headless);


    [TestMethod]
    [TestCategory(Categories.ConsoleApp)]
    public void Pause_Idempotent() => GamingTest.Run(FuncRule.Create(async () =>
    {
        var period = 500;
        var expected = period * .5f;
        var minExpected = period * .48f;
        var maxExpected = period * .58f;

        var wallClockTask = Task.Delay(period);

        await Task.Delay(period / 2);
        Game.Current.Pause();
        Game.Current.Pause();
        Game.Current.Pause();
        await wallClockTask;
        var now = Game.Now;
        Console.WriteLine($"Pause expected between {minExpected} and {maxExpected}, actual: {Math.Round(now.TotalMilliseconds)}");
        Assert.IsTrue(Game.Now.TotalMilliseconds >= minExpected);
        Assert.IsTrue(Game.Now.TotalMilliseconds <= maxExpected);
        await Task.Delay(100);
        Assert.AreEqual(now, Game.Now);
        Game.Current.Resume();
        Game.Current.Resume();
        Game.Current.Resume();
        await Task.Delay(100);
        Assert.AreNotEqual(now, Game.Now);
        Game.Current.Stop();
    }), TestContext.TestId(), UITestMode.Headless);

    [TestMethod]
    [TestCategory(Categories.ConsoleApp)]
    public void Pause_ResumeDoesNotThrowIfPauseLifetimeWasAlreadyDisposed() => GamingTest.Run(FuncRule.Create(async () =>
    {
        Game.Current.Pause();
        Assert.IsTrue(Game.Current.IsPaused);

        var pauseLifetime = Game.Current.PauseManager.PauseLifetime;
        Assert.IsNotNull(pauseLifetime);

        var pauseLeaseField = typeof(PauseManager).GetField("pauseLease", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(pauseLeaseField);

        var pauseLease = pauseLeaseField.GetValue(Game.Current.PauseManager);
        Assert.IsNotNull(pauseLease);

        var recyclableProperty = pauseLease.GetType().GetProperty("Recyclable");
        Assert.IsNotNull(recyclableProperty);

        var backingLifetime = recyclableProperty.GetValue(pauseLease) as Recyclable;
        Assert.IsNotNull(backingLifetime);
        backingLifetime.Dispose();

        Game.Current.Resume();

        Assert.IsFalse(Game.Current.IsPaused);
        Game.Current.Stop();
    }), TestContext.TestId(), UITestMode.Headless);
}

