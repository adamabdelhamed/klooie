
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace klooie.tests;



[TestClass]
[TestCategory(Categories.Observability)]
public class SourceGeneratorBasedObservabilityTests
{

    [TestMethod]
    public void TestGeneratedCode()
    {
        var observable = new SGObservable();

        int nameChangedCount = 0;
        ILifetime propValLt;
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            observable.NameChanged.Subscribe(() => nameChangedCount++, lt);  
            observable.Name = "new name";
            Assert.AreEqual(1, nameChangedCount);
            Assert.AreEqual("new name", observable.Name);
        }
        finally
        {
            lt.Dispose();
        }
        propValLt = observable.NameChanged.CreateNextFireLifetime();
        var lease = propValLt.Lease;
        Assert.IsTrue(propValLt.IsStillValid(lease));
        observable.Name = "new name2";
        var a = 1;
        Assert.IsFalse(propValLt.IsStillValid(lease));
        Assert.AreEqual(1, nameChangedCount);
        Assert.AreEqual("new name2", observable.Name);
    }
}

public partial class SGObservable : IObservableObject
{
    public partial string Name { get; set; }
}
