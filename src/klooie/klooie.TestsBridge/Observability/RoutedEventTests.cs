using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArgsTests.CLI.Games
{
    [TestClass]
    [TestCategory(Categories.Observability)]
    public class RoutedEventTests
    {
        [TestMethod]
        public void TestRoutedEvents()
        {
            var routedEvent = new EventRouter<string>();

            var assertionCount = 0;

            var assertionIncrementCheck = new Action<Action>((a) =>
            {
                var origCount = assertionCount;
                a();
                Assert.AreEqual(origCount + 1, assertionCount);
            });

            routedEvent.RegisterOnce("Home/{Page}", (args) =>
            {
                Assert.AreEqual("thepage", args.RouteVariables["page"]);
                Assert.AreEqual(args.Data, "Foo");
                assertionCount++;
            });

            assertionIncrementCheck(() =>
            {
                routedEvent.Route("Home/ThePage", "Foo");
            });
            Console.WriteLine(assertionCount);
        }
        
    }
}
