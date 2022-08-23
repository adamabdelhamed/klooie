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
            CliTestHarness.SetConsoleSize(9, 1);
            var app = new CliTestHarness(this.TestContext);

            app.Invoke(async () =>
            {
                app.LayoutRoot.Add(new TextBox() { Value = "SomeText".ToWhite() }).Fill();
                await app.RequestPaintAsync();
                Assert.IsTrue(app.Find("SomeText".ToWhite()).HasValue);
                app.Stop();
            });

            app.Run();
            app.AssertThisTestMatchesLKG();
        }

   
    }
}
