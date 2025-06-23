using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.ConsoleApp)]
public class EventLoopTests
{
    [TestMethod]
     public void EventLoop_Basic()
     {
        var loop = new EventLoop();
        loop.Invoke(async () =>
        {
            var tid = Thread.CurrentThread.ManagedThreadId;
            await Task.Delay(1);
            Assert.AreEqual(tid, Thread.CurrentThread.ManagedThreadId);
            loop.Stop();
        });
        loop.Run();
     }

    [TestMethod]
    public void EventLoop_BasicAsync()
    {
        var loop = new EventLoop();
        loop.Invoke(async () =>
        {
            var tid = Thread.CurrentThread.ManagedThreadId;
            await Task.Delay(1);
            Assert.AreEqual(tid, Thread.CurrentThread.ManagedThreadId);
            loop.Stop();
        });
        loop.Run();
    }

    [TestMethod]
    public void EventLoop_InvokeNextCycle()
    {
        var loop = new EventLoop();
        loop.Invoke(() =>
        {
            Assert.AreEqual(0, loop.Cycle);
            loop.InvokeNextCycle(() =>
            {
                Assert.AreEqual(1, loop.Cycle);
                loop.InvokeNextCycle(async() =>
                {
                    Assert.AreEqual(2, loop.Cycle);
                    loop.Stop();
                });
            });
        });
        loop.Run();
    }

    [TestMethod]
    public void EventLoop_For()
    {
        var loop = new EventLoop();
        int count = 0;
        var iters = 10;
        var delayMs = 10;
        loop.InnerLoopAPIs.For(iters, delayMs, (i) => Assert.AreEqual(i, count++), loop.Stop);
        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();
        Assert.AreEqual(iters, count);

        var expectedDuration = iters * delayMs;
        var tolerance = expectedDuration * 0.2;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void EventLoop_ForScoped()
    {
        var loop = new EventLoop();
        int count = 0;
        var iters = 10;
        var delayMs = 10;
        var scope = new object();
        loop.InnerLoopAPIs.For(iters, delayMs, scope, (i, s) => Assert.AreEqual(i, count++), s => loop.Stop());
        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();
        Assert.AreEqual(iters, count);

        var expectedDuration = iters * delayMs;
        var tolerance = expectedDuration * 0.1;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void EventLoop_Do()
    {
        var loop = new EventLoop();
        int count = 0;
        var iters = 10;
        var delayMs = 10;
        loop.InnerLoopAPIs.Do(delayMs, ()=> ++count == iters ? DoReturnType.Break : DoReturnType.Continue, loop.Stop);

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();
        Assert.AreEqual(iters, count);

        var expectedDuration = (iters-1) * delayMs;
        var tolerance = expectedDuration * 0.1;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void EventLoop_DoScoped()
    {
        var loop = new EventLoop();
        int count = 0;
        var iters = 10;
        var delayMs = 10;
        var scope = new object();
        loop.InnerLoopAPIs.Do(delayMs, scope, (s) => ++count == iters ? DoReturnType.Break : DoReturnType.Continue, s => loop.Stop());

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();
        Assert.AreEqual(iters, count);

        var expectedDuration = (iters - 1) * delayMs;// -1 since we break on the last iteration
        var tolerance = expectedDuration * 0.1;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void EventLoop_Delay()
    {
        var loop = new EventLoop();
        var expectedDuration = 100;
        loop.InnerLoopAPIs.Delay(expectedDuration, loop.Stop);

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();



        var tolerance = expectedDuration * 0.1;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Expected duration: {expectedDuration}, Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void EventLoop_DelayScoped()
    {
        var loop = new EventLoop();
        var obj = new object();
        var expectedDuration = 100;
        loop.InnerLoopAPIs.Delay(expectedDuration, obj, o =>
        {
            Assert.AreSame(obj, o);
            loop.Stop();
        });

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();



        var tolerance = expectedDuration * 0.1;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Expected duration: {expectedDuration}, Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }
}

