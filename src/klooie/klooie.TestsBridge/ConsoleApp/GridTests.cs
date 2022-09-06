using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace ArgsTests.CLI
{
    [TestClass]
    [TestCategory(Categories.Slow)]
    public class GridTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestBasicGrid() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
         {
             var grid = new Grid(new List<object>
              {
                new { Name = "Adam", State = "Washington" },
                new { Name = "Bob", State = "New Jersey" }
              });
             ConsoleApp.Current.LayoutRoot.Add(grid).Fill();
             grid.Focus();
             await context.PaintAndRecordKeyFrameAsync();
             await ConsoleApp.Current.SendKey(ConsoleKey.DownArrow);
             await context.PaintAndRecordKeyFrameAsync();
             await ConsoleApp.Current.SendKey(ConsoleKey.UpArrow);
             await context.PaintAndRecordKeyFrameAsync();
             ConsoleApp.Current.Stop();
         });
    }
}
