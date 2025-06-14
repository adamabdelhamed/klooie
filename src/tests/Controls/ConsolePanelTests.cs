﻿using klooie;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace tests;
[TestClass]
[TestCategory(Categories.ConsoleApp)]
[TestCategory(Categories.Quarantined)] // Until I get pooling under control
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
        foreach(var pool in poolsWithPendingReturns)
        {
            Console.WriteLine($"{DefaultRecyclablePool.GetFriendlyName(pool.GetType())}: Pending: {pool.Rented - pool.Returned}");
        }
        Assert.AreEqual(0,poolsWithPendingReturns.Length);
#endif
    }
}

