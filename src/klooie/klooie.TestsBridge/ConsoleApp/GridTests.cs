using klooie;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
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
       public void TestBasicGrid()
       {
            var grid = new Grid(new List<object>
            {
                new { Name = "Adam", State = "Washington" },
                new { Name = "Bob", State = "New Jersey" }
            });

            CliTestHarness.SetConsoleSize(80, 30);
            var app = new CliTestHarness(this.TestContext);

            app.Invoke(() =>
            {
                app.LayoutRoot.Add(grid).Fill();
                grid.Focus();
                app.SetTimeout(() => app.SendKey(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false,false,false)), TimeSpan.FromMilliseconds(333));
                app.SetTimeout(() => app.SendKey(new ConsoleKeyInfo((char)0, ConsoleKey.UpArrow, false, false, false)), TimeSpan.FromMilliseconds(666));
                app.SetTimeout(() => app.Stop(), TimeSpan.FromMilliseconds(1000));
            });
            app.Start().Wait();

            app.AssertThisTestMatchesLKG();
        }
    }
}
