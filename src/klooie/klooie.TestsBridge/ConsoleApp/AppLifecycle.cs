using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
using PowerArgs;
using System.Threading.Tasks;
using System.Linq;
using klooie;

namespace ArgsTests.CLI.Apps
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class AppLifecycle
    {
        [TestMethod]
        public void PumpFailurePreservesStack()
        {
           // var testCli = new klooie.tests.KlooieTestConsole(80, 4);
           // ConsoleProvider.Current = testCli;
            ConsoleApp app = new ConsoleApp();
            var task = app.Start();

            app.InvokeNextCycle(() =>
            {
                throw new FormatException("Some fake exception");
            });

            try
            {
                task.Wait();
                Assert.Fail("An exception should have been thrown");
            }
            catch (AggregateException ex)
            {
                var cleaned = ex.Clean().Single();
                Assert.AreEqual(typeof(FormatException), cleaned.GetType());
                Assert.AreEqual("Some fake exception", cleaned.Message);
            }
        }
    }
}
