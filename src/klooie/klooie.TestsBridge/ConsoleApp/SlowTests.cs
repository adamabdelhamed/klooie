using klooie;
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
        public void TestThreeMonthCalendarBasicRender()
        {
            CliTestHarness.SetConsoleSize(120,40);
            var app = new CliTestHarness(this.TestContext);
            app.Invoke(async () =>
            {
                var carousel = new ThreeMonthCarousel(new ThreeMonthCarouselOptions() { Month = 1, Year = 2000 });
                var start = carousel.Options.Month + "/" + carousel.Options.Year;
                app.LayoutRoot.Add(new FixedAspectRatioPanel(4f / 1f, carousel)).Fill();
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
                app.Stop();
            });
            app.Run();
            app.AssertThisTestMatchesLKGFirstAndLastFrame();
        }

        [TestMethod]
        public void TestTextBoxBlinkState()
        {
            CliTestHarness.SetConsoleSize(9,1);
            var app = new CliTestHarness(this.TestContext);
            app.Invoke(() =>
            {
                app.LayoutRoot.Add(new TextBox() { Value = "SomeText".ToWhite() }).Fill();
                app.SetTimeout(() => app.Stop(), TimeSpan.FromSeconds(.9f));
            });
            app.Start().Wait();
            app.AssertThisTestMatchesLKG();
        }

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
