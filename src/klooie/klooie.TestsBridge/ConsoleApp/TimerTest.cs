using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
using PowerArgs;
using klooie;

namespace ArgsTests.CLI
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class TimerTest
    {
        [TestMethod]
        public void TestSetTimeout()
        {
            CliTestHarness.SetConsoleSize(1, 1);
            var app = new ConsoleApp();
            var promise = app.Start();
            var count = 0;
            app.SetTimeout(() => { count++; app.Stop(); }, TimeSpan.FromMilliseconds(50));
            promise.Wait();
            Assert.AreEqual(1, count);
        }

      

        [TestMethod]
        public void TestSetInterval()
        {
            var app = new ConsoleApp();
            var promise = app.Start();
            var count = 0;
            app.SetInterval(() => { count++; if (count == 5) { app.Stop(); } }, TimeSpan.FromMilliseconds(50));
            promise.Wait();
            Assert.AreEqual(5, count);
        }

        [TestMethod]
        [Timeout(1000)]
        public void TestSetIntervalCancelling()
        {
            var app = new ConsoleApp();
 
            var count = 0;
            IDisposable handle = null;
            handle = app.SetInterval(async () => 
            {
                count++; 
                if (count == 5) 
                {
                    handle.Dispose();
                    await Task.Delay(20);
                    app.Stop();
                }
            }, TimeSpan.FromMilliseconds(5));
            app.Run();
            Assert.AreEqual(5, count);
        }

        [TestMethod]
        public async Task TestTaskTimeouts()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).TimeoutAfter(TimeSpan.FromMilliseconds(1));
                Assert.Fail("An exception should have been thrown");
            }
            catch (TimeoutException) { }

            try
            {
                var unusedResult = await Task<string>.Factory.StartNew(()=> { Thread.Sleep(100); return "Hello"; }).TimeoutAfter(TimeSpan.FromMilliseconds(1));
                Assert.Fail("An exception should have been thrown");
            }
            catch (TimeoutException) { }

            var stringResult = await Task<string>.Factory.StartNew(() => {  return "Hello"; }).TimeoutAfter(TimeSpan.FromSeconds(1));
            Assert.AreEqual("Hello", stringResult);
        }
    }
}
