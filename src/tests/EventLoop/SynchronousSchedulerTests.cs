using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.EventLoop)]
public class SynchronousSchedulerTests
{
    [TestMethod]
    public void SynchronousScheduler_Delay()
    {
        TestContextHelper.GlobalSetup();
        var loop = new ConsoleApp();
        var expectedDuration = 5f * 1000 / LayoutRootPanel.MaxPaintRate;
        var pool = SyncronousScheduler.StatelessWorkItem.pool.Value;
        var created = pool.Created;
        var rented = pool.Rented;
        var returned = pool.Returned;
        loop.Invoke(() => loop.Scheduler.Delay(expectedDuration, loop.Stop));

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();

        Assert.AreEqual(rented + 1, pool.Rented);
        Assert.AreEqual(returned + 1, pool.Returned);


        var tolerance = expectedDuration * 0.2;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Expected duration: {expectedDuration}, Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void SynchronousScheduler_DelayScoped()
    {
        TestContextHelper.GlobalSetup();
        var loop = new ConsoleApp();
        var obj = new object();
        var expectedDuration = 5f * 1000 / LayoutRootPanel.MaxPaintRate;

        var pool = SyncronousScheduler.StatefulWorkItem<object>.pool.Value;
        var created = pool.Created;
        var rented = pool.Rented;
        var returned = pool.Returned;

        loop.Invoke(() => loop.Scheduler.Delay(expectedDuration, obj, o =>
        {
            Assert.AreSame(obj, o);
            loop.Stop();
        }));

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();


        Assert.AreEqual(rented + 1, pool.Rented);
        Assert.AreEqual(returned + 1, pool.Returned);

        var tolerance = expectedDuration * 0.2;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Expected duration: {expectedDuration}, Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void SynchronousScheduler_DelayIfValid_Valid()
    {
        TestContextHelper.GlobalSetup();
        var loop = new ConsoleApp();
        var state = DelayState.Create(loop);
        var expectedDuration = 5f * 1000 / LayoutRootPanel.MaxPaintRate;

        var workItemPool = SyncronousScheduler.StatefulWorkItem<SyncronousScheduler.DelayIfValidInstance<DelayState>>.pool.Value;
        var workItemsCreatedBefore = workItemPool.Created;
        var workItemsRentedBefore = workItemPool.Rented;
        var workItemsReturnedBefore = workItemPool.Returned;

        var delayIfValidInstancePool = SyncronousScheduler.DelayIfValidInstance<DelayState>.pool.Value;
        var delayIfValidInstancesCreatedBefore = delayIfValidInstancePool.Created;
        var delayIfValidInstancesRentedBefore = delayIfValidInstancePool.Rented;
        var delayIfValidInstancesReturnedBefore = delayIfValidInstancePool.Returned;

        loop.Invoke(() => loop.Scheduler.DelayIfValid(expectedDuration, state, o =>
        {
            Assert.IsTrue(state.IsStillValid(state.Lease), "Should be alive at this point");
            Assert.AreSame(state, o);
            loop.Stop();
        }));

        var sw = Stopwatch.StartNew();
        loop.Run();
        sw.Stop();
        Assert.IsFalse(state.IsStillValid(state.Lease), "Should be disposed on app disposal");

        Assert.IsTrue(workItemPool.Created - workItemsCreatedBefore < 2, "We will create either zero or one, depending on which other tests have run");
        Assert.AreEqual(workItemsRentedBefore + 1, workItemPool.Rented);
        Assert.AreEqual(workItemsReturnedBefore + 1, workItemPool.Returned);

        Assert.IsTrue(delayIfValidInstancePool.Created - delayIfValidInstancesCreatedBefore < 2, "We will create either zero or one, depending on which other tests have run");
        Assert.AreEqual(delayIfValidInstancesRentedBefore + 1, delayIfValidInstancePool.Rented);
        Assert.AreEqual(delayIfValidInstancesReturnedBefore + 1, delayIfValidInstancePool.Returned);

        var tolerance = expectedDuration * 0.2;
        var minAcceptableDuration = expectedDuration - tolerance;
        var maxAcceptableDuration = expectedDuration + tolerance;
        Console.WriteLine($"Expected duration: {expectedDuration}, Actual duration: {sw.ElapsedMilliseconds}ms");
        Assert.IsTrue(sw.ElapsedMilliseconds >= minAcceptableDuration);
        Assert.IsTrue(sw.ElapsedMilliseconds <= maxAcceptableDuration);
    }

    [TestMethod]
    public void SynchronousScheduler_DelayIfValid_Invalid()
    {
        TestContextHelper.GlobalSetup();
        var loop = new ConsoleApp();
        var state = DelayState.Create(loop);

        var workItemPool = SyncronousScheduler.StatefulWorkItem<SyncronousScheduler.DelayIfValidInstance<DelayState>>.pool.Value;
        var workItemsCreatedBefore = workItemPool.Created;
        var workItemsRentedBefore = workItemPool.Rented;
        var workItemsReturnedBefore = workItemPool.Returned;

        var delayIfValidInstancePool = SyncronousScheduler.DelayIfValidInstance<DelayState>.pool.Value;
        var delayIfValidInstancesCreatedBefore = delayIfValidInstancePool.Created;
        var delayIfValidInstancesRentedBefore = delayIfValidInstancePool.Rented;
        var delayIfValidInstancesReturnedBefore = delayIfValidInstancePool.Returned;

        state.Dispose();
        loop.Invoke(() => loop.Scheduler.DelayIfValid(100, state, o => throw new Exception("This should not happen because the state should be disposed")));
        loop.Invoke(() => loop.Scheduler.Delay(200, loop, static loop => loop.Stop()));

        loop.Run();
        Assert.IsFalse(state.IsStillValid(state.Lease), "Should still be disposed");

        Assert.IsTrue(workItemPool.Created - workItemsCreatedBefore < 2, "We will create either zero or one, depending on which other tests have run");
        Assert.AreEqual(workItemsRentedBefore + 1, workItemPool.Rented);
        Assert.AreEqual(workItemsReturnedBefore + 1, workItemPool.Returned);

        Assert.IsTrue(delayIfValidInstancePool.Created - delayIfValidInstancesCreatedBefore < 2, "We will create either zero or one, depending on which other tests have run");
        Assert.AreEqual(delayIfValidInstancesRentedBefore + 1, delayIfValidInstancePool.Rented);
        Assert.AreEqual(delayIfValidInstancesReturnedBefore + 1, delayIfValidInstancePool.Returned);
    }
}
