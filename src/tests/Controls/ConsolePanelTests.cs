using klooie;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tests;
[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ConsolePanelTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize() => TestContextHelper.GlobalSetup();
    [TestMethod]
    public void ConsoleApp_PoolCheck()
    {
        PoolManager.Instance.ClearAll();
        ConsoleApp app = new ConsoleApp();
        app.Invoke(async () =>
        {
            await Task.Delay(10);
            app.Stop();
        });

        app.Run();
#if DEBUG
        var poolsWithPendingReturns = PoolManager.Instance.Pools.Where(p => p.Rented != p.Returned).OrderByDescending(p => p.Rented - p.Returned).ToArray();
        Assert.AreEqual(0,poolsWithPendingReturns.Length);
#endif
    }
}

