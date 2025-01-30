using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Observability)]
public partial class ObservabilityTests
{
    public partial class SomeOtherObservable : Recyclable, IObservableObject
    {
        public partial string Name { get; set; }
    }

    public partial class SomeObservable : Recyclable, IObservableObject
    {
        public Event SomeEvent { get; private set; } = new Event();
        public Event<string> SomeEventWithAString { get; private set; } = new Event<string>();

        public ObservableCollection<string> Strings { get; private set; } = new ObservableCollection<string>();

        public ObservableCollection<SomeOtherObservable> Children { get; private set; } = new ObservableCollection<SomeOtherObservable>();


        public partial string Name { get; set; }
        public partial int Number { get; set; }
    }


    [TestMethod]
    public void SubscribeForLifetimeToProperty()
    {
        var observable = new SomeObservable();

        var triggerCount = 0;

        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {
            observable.NameChanged.Subscribe(() =>
            {
                triggerCount++;
            }, lifetime);

            Assert.AreEqual(0, triggerCount);
            observable.Name = "Some value";
            Assert.AreEqual(1, triggerCount);
        }
        finally
        {
            lifetime.Dispose();
        }

        observable.Name = "Some new value";
        Assert.AreEqual(1, triggerCount);
    }

    [TestMethod]
    public void SubscribeToProperty()
    {
        var observable = new SomeObservable();

        var triggerCount = 0;

        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {
            observable.NameChanged.Subscribe(() =>
            {
                triggerCount++;
            }, lifetime);

            Assert.AreEqual(0, triggerCount);
            observable.Name = "Some value";
            Assert.AreEqual(1, triggerCount);
        }
        finally
        {
            lifetime.Dispose();
        }

        observable.Name = "Some new value again";
        Assert.AreEqual(1, triggerCount);
    }

    [TestMethod]
    public void SubscribeOnceToProperty()
    {
        var observable = new SomeObservable();
        var triggerCount = 0;
        observable.NameChanged.SubscribeOnce(() => { triggerCount++; });
        Assert.AreEqual(0, triggerCount);
        observable.Name = "Some value";
        Assert.AreEqual(1, triggerCount);
        observable.Name = "Some new value again";
        Assert.AreEqual(1, triggerCount);
    }

    [TestMethod]
    public void SubscribeToAllProperties()
    {
        var observable = new SomeObservable();
        int numChanged = 0;

        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {

            observable.SubscribeToAnyPropertyChange(this, (o) => { numChanged++; }, lifetime);

            Assert.AreEqual(0, numChanged);
            observable.Name = "Foo";
            Assert.AreEqual(1, numChanged);
            observable.Number = 1;
            Assert.AreEqual(2, numChanged);
        }
        finally
        {
            lifetime.Dispose();
        }
        Assert.AreEqual(2, numChanged);
        observable.Name = "Foo2";
        Assert.AreEqual(2, numChanged);
        observable.Number = 2;
        Assert.AreEqual(2, numChanged);
    }


    [TestMethod]
    public void SubscribeUnmanagedToEventOfStringWithUnsubscribe()
    {
        var observable = new SomeObservable();
        var triggerCount = 0;
        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {
            observable.SomeEventWithAString.Subscribe((s) => { triggerCount++; }, lifetime);

            Assert.AreEqual(0, triggerCount);
            observable.SomeEventWithAString.Fire("Foo");
            Assert.AreEqual(1, triggerCount);
        }
        finally
        {
            lifetime.Dispose();
        }
        observable.SomeEventWithAString.Fire("Foo");
        Assert.AreEqual(1, triggerCount);
    }

    [TestMethod]
    public void SubscribeForLifetimeToEvent()
    {
        var observable = new SomeObservable();

        var triggerCount = 0;

        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {
            observable.SomeEvent.Subscribe(() => { triggerCount++; }, lifetime);

            Assert.AreEqual(0, triggerCount);
            observable.SomeEvent.Fire();
            Assert.AreEqual(1, triggerCount);
        }
        finally
        {
            lifetime.Dispose();
        }
        observable.SomeEvent.Fire();
        Assert.AreEqual(1, triggerCount);
    }

    [TestMethod]
    public void SubscribeToEvent()
    {
        var observable = new SomeObservable();

        var triggerCount = 0;

        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {

            observable.SomeEvent.Subscribe(() => { triggerCount++; }, lifetime);

            Assert.AreEqual(0, triggerCount);
            observable.SomeEvent.Fire();
            Assert.AreEqual(1, triggerCount);


            observable.SomeEvent.Fire();
            Assert.AreEqual(2, triggerCount);
        }
        finally
        {
            lifetime.Dispose();
        }
        observable.SomeEvent.Fire();
        Assert.AreEqual(2, triggerCount);
    }

    [TestMethod]
    public void SynchronizeCollection()
    {
        var observable = new SomeObservable();
        int addCalls = 0, removeCalls = 0, changedCalls = 0;

        observable.Strings.Add("a");
        observable.Strings.Add("b");


        var lifetime = DefaultRecyclablePool.Instance.Rent();
        try
        {
            observable.Strings.Sync((s) => { addCalls++; }, (s) => { removeCalls++; }, () => { changedCalls++; }, lifetime);

            Assert.AreEqual(2, addCalls);
            Assert.AreEqual(0, removeCalls);
            Assert.AreEqual(1, changedCalls);

            observable.Strings.Add("c");
            Assert.AreEqual(3, addCalls);
            Assert.AreEqual(0, removeCalls);
            Assert.AreEqual(2, changedCalls);

            observable.Strings.Remove("a");
            Assert.AreEqual(3, addCalls);
            Assert.AreEqual(1, removeCalls);
            Assert.AreEqual(3, changedCalls);
        }
        finally
        {
            lifetime.Dispose();
        }
        observable.Strings.Add("d");
        observable.Strings.Remove("d");
        Assert.AreEqual(3, addCalls);
        Assert.AreEqual(1, removeCalls);
        Assert.AreEqual(3, changedCalls);
    }

    [TestMethod]
    public void SubscribeToChildren()
    {
        var observable = new SomeObservable();
        var existinChild = new SomeOtherObservable();
        observable.Children.Add(existinChild);

        int numChildrenChanged = 0;
        int numChildrenAdded = 0;
        int numChildrenRemoved = 0;
        observable.Children.Sync((c) =>
        {
            c.NameChanged.Sync(() =>
            {
                numChildrenChanged++;
            }, observable.Children.GetMembershipLifetime(c));
            numChildrenAdded++;
        },
        (c) =>
        {
            numChildrenRemoved++;
        },
        () =>
        {
        }, observable);

        var newItem = new SomeOtherObservable();

        observable.Children.Add(newItem);

        Assert.AreEqual(2, numChildrenChanged);
        existinChild.Name = "Change";
        Assert.AreEqual(3, numChildrenChanged);

        newItem.Name = "Second change";
        Assert.AreEqual(4, numChildrenChanged);

        observable.Children.Remove(existinChild);
        existinChild.Name = "Ignored change";
        Assert.AreEqual(4, numChildrenChanged);

        observable.Children.Remove(newItem);
        newItem.Name = "Ignored change";
        Assert.AreEqual(4, numChildrenChanged);

        Assert.AreEqual(2, numChildrenAdded);
        Assert.AreEqual(2, numChildrenRemoved);
    }

    [TestMethod]
    public void TestObservableCollectionMembership()
    {
        var collection = new ObservableCollection<object>();
        var obj = new object();
        collection.Add(obj);
        var lifetime = collection.GetMembershipLifetime(obj);
        Assert.IsNotNull(lifetime);
        Assert.IsTrue(lifetime.ShouldContinue);
        Assert.IsFalse(lifetime.IsExpired);
        Assert.IsFalse(lifetime.ShouldStop);
        Assert.IsFalse(lifetime.IsExpiring);
        collection.Remove(obj);
        Assert.IsFalse(lifetime.ShouldContinue);
        Assert.IsTrue(lifetime.IsExpired);
        Assert.IsTrue(lifetime.ShouldStop);
        Assert.IsFalse(lifetime.IsExpiring);
    }


    [TestMethod]
    public void TestSubscribeOnce()
    {
        var ev = new Event();
        var counter = 0;
        var count = () =>
        {
            counter++;
        };
        ev.SubscribeOnce(count);
        Assert.IsTrue(ev.HasSubscriptions);
        Assert.AreEqual(0, counter);
        ev.Fire();
        Assert.IsFalse(ev.HasSubscriptions);
        Assert.AreEqual(1, counter);
        ev.Fire();
        Assert.AreEqual(1, counter);
    }
 

    [TestMethod]
    public void TestDisposal()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        var fired = false;
        lt.OnDisposed(() => fired = true);
        Assert.IsFalse(fired);
        lt.Dispose();
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void TestRecycle()
    {
        var recyclable = DefaultRecyclablePool.Instance.Rent();

        Assert.IsFalse(recyclable.IsExpired);
        Assert.IsFalse(recyclable.IsExpiring);

        recyclable.Dispose();

        var reRented = DefaultRecyclablePool.Instance.Rent();
        Assert.AreSame(recyclable, reRented);
        Assert.IsFalse(reRented.IsExpired);
        Assert.IsFalse(reRented.IsExpiring);
    }
}
