using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArgsTests.CLI
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class VisualTreeTests
    {
        [TestMethod]
        [TestCategory(Categories.ConsoleApp)]
        public void ConsoleAppLifecycleTestBasic()
        {
            ConsoleApp app = new ConsoleApp();
            app.Invoke(() =>
            {
                int addCounter = 0, removeCounter = 0;

                app.ControlAdded.Subscribe((c) => { addCounter++; }, app);
                app.ControlRemoved.Subscribe((c) => { removeCounter++; }, app);
                ConsolePanel panel = app.LayoutRoot.Add(new ConsolePanel());
                // direct child
                Assert.AreEqual(1, addCounter);
                Assert.AreEqual(0, removeCounter);

                var button = panel.Add(new Button());

                // grandchild
                Assert.AreEqual(2, addCounter);
                Assert.AreEqual(0, removeCounter);

                var innerPanel = new ConsolePanel();
                var innerInnerPanel = innerPanel.Add(new ConsolePanel());

                // no change since not added to the app yet
                Assert.AreEqual(2, addCounter);
                Assert.AreEqual(0, removeCounter);

                panel.Add(innerPanel);

                // both child and grandchild found on add
                Assert.AreEqual(4, addCounter);
                Assert.AreEqual(0, removeCounter);

                // remove a nested child
                innerPanel.Controls.Remove(innerInnerPanel);
                Assert.AreEqual(4, addCounter);
                Assert.AreEqual(1, removeCounter);

                app.LayoutRoot.Controls.Clear();
                Assert.AreEqual(4, addCounter);
                Assert.AreEqual(4, removeCounter);
                app.Stop();
            });
            app.Run();
        }

        
        [TestMethod]
        public void EnsureCantReuseControls()
        {
            ConsoleApp app = new ConsoleApp();
            app.Invoke(() =>
            {
                var panel = app.LayoutRoot.Add(new ConsolePanel());
                var button = panel.Add(new Button());
                panel.Controls.Remove(button);

                try
                {
                    app.LayoutRoot.Add(button);
                    Assert.Fail("An exception should have been thrown");
                }
                catch (ObjectDisposedException ex)
                {
                    Console.WriteLine(ex.Message);
                    app.Stop();
                }
            });
            app.Run();
        }
    }
}
