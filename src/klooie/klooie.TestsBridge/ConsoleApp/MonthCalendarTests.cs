using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
using System;

namespace ArgsTests.CLI.Controls
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class MonthCalendarTests
    {
        public TestContext TestContext { get; set; }
        
        [TestMethod]
        public void TestMonthCalendarBasicRender() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, MonthCalendar.MinWidth, MonthCalendar.MinHeight,async(context)=>
        { 
            ConsoleApp.Current.LayoutRoot.Add(new MonthCalendar(new MonthCalendarOptions() { Year = 2000, Month = 1 })).Fill();
            await context.PaintAndRecordKeyFrameAsync();
            ConsoleApp.Current.Stop();
        });

        [TestMethod]
        public void TestMonthCalendarFocusAndNav() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, MonthCalendar.MinWidth, MonthCalendar.MinHeight, async (context) =>
        {
            var calendar = ConsoleApp.Current.LayoutRoot.Add(new MonthCalendar(new MonthCalendarOptions() { Year = 2000, Month = 1 })).Fill();
            await context.PaintAndRecordKeyFrameAsync();
            calendar.Focus();
            await context.PaintAndRecordKeyFrameAsync();
            var fwInfo = new ConsoleKeyInfo('a', calendar.Options.AdvanceMonthForwardKey.Key, false, false, false);
            var backInfo = new ConsoleKeyInfo('b', calendar.Options.AdvanceMonthBackwardKey.Key, false, false, false);
            await ConsoleApp.Current.SendKey(backInfo);
            await context.PaintAndRecordKeyFrameAsync();
            await ConsoleApp.Current.SendKey(fwInfo);
            await context.PaintAndRecordKeyFrameAsync();
            ConsoleApp.Current.Stop();
        }); 
    }
}
