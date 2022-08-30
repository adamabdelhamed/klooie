using klooie;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using PowerArgs.Cli;
using System;
using System.Threading.Tasks;

namespace ArgsTests.CLI
{
    [TestClass]
    [TestCategory(Categories.Slow)]
    public class SlowTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestThreeMonthCalendarBasicRender() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI,120, 40,async(context)=>
        {
            var carousel = new ThreeMonthCarousel(new ThreeMonthCarouselOptions() { Month = 1, Year = 2000 });
            var start = carousel.Options.Month + "/" + carousel.Options.Year;
            ConsoleApp.Current.LayoutRoot.Add(new FixedAspectRatioPanel(4f / 1f, carousel)).Fill();
            Assert.IsTrue(await carousel.SeekAsync(true, carousel.Options.AnimationDuration));
            await Task.Delay(1000);
            Assert.IsTrue(await carousel.SeekAsync(true, carousel.Options.AnimationDuration));
            await Task.Delay(1000);
            Assert.IsTrue(await carousel.SeekAsync(true, carousel.Options.AnimationDuration));
            await Task.Delay(1000);
            Assert.IsTrue(await carousel.SeekAsync(true, carousel.Options.AnimationDuration));

            await Task.Delay(3000);

            var now = carousel.Options.Month + "/" + carousel.Options.Year;
            Assert.AreNotEqual(start, now);

            Assert.IsTrue(await carousel.SeekAsync(false, carousel.Options.AnimationDuration));
            await Task.Delay(1000);
            Assert.IsTrue(await carousel.SeekAsync(false, carousel.Options.AnimationDuration));
            await Task.Delay(1000);
            Assert.IsTrue(await carousel.SeekAsync(false, carousel.Options.AnimationDuration));
            await Task.Delay(1000);
            Assert.IsTrue(await carousel.SeekAsync(false, carousel.Options.AnimationDuration));
            await Task.Delay(1000);

            now = carousel.Options.Month + "/" + carousel.Options.Year;
            Assert.AreEqual(start, now);
            ConsoleApp.Current.Stop();
        });


        [TestMethod]
        public async Task TestTaskTimeout()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).TimeoutAfter(TimeSpan.FromSeconds(.5));
                Assert.Fail("An exception should have been thrown");
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Expected timeout fired");
            }
        }
    }
}
