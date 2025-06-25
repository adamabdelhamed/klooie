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
    public void EventLoop_Delay()
    {
        var loop = new EventLoop();
        var expectedDuration = 100;
        loop.Scheduler.Delay(expectedDuration, loop.Stop);

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
        loop.Scheduler.Delay(expectedDuration, obj, o =>
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

