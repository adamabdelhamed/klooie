﻿
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
        var observable = new SomeObservable();

        int nameChangedCount = 0;
        ILifetimeManager propValLt;
        using(var lt = new Lifetime())
        {
            observable.NameChanged.Subscribe(() => nameChangedCount++, lt);  
            observable.Name = "new name";
            Assert.AreEqual(1, nameChangedCount);
            Assert.AreEqual("new name", observable.Name);
        }
        propValLt = observable.GetPropertyValueLifetime(nameof(SomeObservable.Name));
        Assert.IsFalse(propValLt.IsExpired);
        observable.Name = "new name2";
        var a = 1;
        Assert.IsTrue(propValLt.IsExpired);
        Assert.AreEqual(1, nameChangedCount);
        Assert.AreEqual("new name2", observable.Name);
    }
}

public partial class SomeObservable : IObservableObject
{
    public partial string Name { get; set; }
}