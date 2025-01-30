using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace klooie.tests
{
    [TestClass]
    [TestCategory(Categories.Slow)]
    public class AllocationTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void ValidateAllocationTestCatchesIncreasedMemory() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
        {
            var objects = new List<object>();
            var exceptionSeen = false;
            try
            {
                await AllocationTestCommon(async () =>
                {
                    objects.Add(new object());
                    await Task.Yield();
                });
            }
            catch(AssertFailedException)
            {
                exceptionSeen = true;
            }
            Assert.IsTrue(exceptionSeen, "An exception should have been thrown");
            ConsoleApp.Current.Stop();
        });

        [TestMethod]
        public void AllocationTest_ConsoleControl() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
        {
            await AllocationTestCommon(async () =>
            {
                var control = ConsoleControlPool.Instance.Rent();
                control.Background = RGB.Red;
                ConsoleApp.Current.LayoutRoot.Add(control);
                await Task.Yield();
                control.Dispose();
            });
        });

        [TestMethod]
        public void AllocationTest_ConsolePanel() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
        {
            await AllocationTestCommon(async () =>
            {
                var control = ConsolePanelPool.Instance.Rent();
                control.Background = RGB.Red;
                ConsoleApp.Current.LayoutRoot.Add(control);
                await Task.Yield();
                control.Dispose();
            });
        });


        [TestMethod]
        public void AllocationTest_GameColliderl() => GamingTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
        {
            await AllocationTestCommon(async () =>
            {
                var control = GameColliderPool.Instance.Rent();
                control.Background = RGB.Red;
                Game.Current.GamePanel.Add(control);
                await Task.Yield();
                control.Dispose();
            });
        });

        private async Task AllocationTestCommon(Func<Task> work)
        {
            long previousIterationMemory = 0;
            long currentMemory = 0;
            var iterations = 50;
            long[] diffs = new long[iterations / 2];
            var diffIndex = 0;
            for (var i = 0; i < iterations; i++)
            {
                await work();

                if (i < iterations / 2)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    previousIterationMemory = GC.GetTotalMemory(forceFullCollection: true);
                    continue;
                }

                currentMemory = GC.GetTotalMemory(forceFullCollection: false);
                diffs[diffIndex++] = Math.Max(0, currentMemory - previousIterationMemory);
                previousIterationMemory = currentMemory;
            }

            var numberOfZeros = 0.0;
            for (var i = 0; i < diffs.Length; i++)
            {
                numberOfZeros += diffs[i] == 0 ? 1 : 0;
            }
            var percentageOfZeros = numberOfZeros / diffs.Length;
            // At least 80 % of the diffs were zero, meaning no memory was allocated
            Console.WriteLine($"{Math.Round(100 * percentageOfZeros, 2)} % of diffs showed zero memory increases. {string.Join(',',diffs)}");
            Assert.IsTrue(percentageOfZeros >= .8);

            ConsoleApp.Current.Stop();
        }
    }
}
