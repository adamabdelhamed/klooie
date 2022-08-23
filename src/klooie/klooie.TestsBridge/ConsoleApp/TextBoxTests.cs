using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
using PowerArgs;
using System.Threading;
using klooie;

namespace ArgsTests.CLI.Controls
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class TextBoxTests
    {
        public TestContext TestContext { get; set; }

    

        [TestMethod]
        public void TestRenderTextBox()
        {
            var app = new CliTestHarness(this.TestContext, 9, 1);

            app.InvokeNextCycle(async () =>
            {
                app.LayoutRoot.Add(new TextBox() { Value = "SomeText".ToWhite() }).Fill();
                await app.RequestPaintAsync();
                Assert.IsTrue(app.Find("SomeText".ToWhite()).HasValue);
                app.Stop();
            });

            app.Start().Wait();
            app.AssertThisTestMatchesLKG();
        }

   
    }
}
