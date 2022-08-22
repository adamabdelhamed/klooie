using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public async Task EventLoop_BasicAsync()
    {
        var loop = new EventLoop();
        loop.Invoke(async () =>
        {
            var tid = Thread.CurrentThread.ManagedThreadId;
            await Task.Delay(1);
            Assert.AreEqual(tid, Thread.CurrentThread.ManagedThreadId);
            loop.Stop();
        });
        await loop.Start();
    }

    [TestMethod]
    public async Task EventLoop_InvokeNextCycle()
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
}

