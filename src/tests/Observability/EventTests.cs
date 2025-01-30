using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace klooie.tests;
[TestClass]
public class EventTests
{
    [TestMethod]
    public void TestEvent()
    {
        var ev = new Event();
        var lt = new Recyclable();
        TestEventMechanics(ev, lt);
    }

    [TestMethod]
    public void TestEventT()
    {
        var ev = new Event<object>();
        var lt = new Recyclable();
        TestEventMechanicsT(ev, lt, new object());
    }

    [TestMethod]
    public void TestEventTScoped()
    {
        object scope = new object();
        int args = 1;
        var ev = new Event<int>();
        var lt = new Recyclable();
        TestEventMechanicsScoped<int>(ev, lt, args, new object());
    }

    [TestMethod]
    public void TestEventScoped()
    {
        object scope = new object();
        var ev = new Event();
        var lt = new Recyclable();
        TestEventMechanics(ev, lt, scope);
    }

    [TestMethod]
    public void TestEventReusability()
    {
        for (var i = 0; i < 10; i++)
        {
            var ev = EventPool.Instance.Rent();
            var lt = DefaultRecyclablePool.Instance.Rent();
            TestEventMechanics(ev, lt);
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void TestEventReusabilityScopedAndUnscoped()
    {
        var scope = new object();
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();

        for (var i = 0; i < 3; i++)
        {
            TestEventMechanics(ev, lt);

            ev.TryDispose();
            lt.TryDispose();
            ev = EventPool.Instance.Rent();
            lt = DefaultRecyclablePool.Instance.Rent();

            TestEventMechanics(ev, lt, scope);

            ev.TryDispose();
            lt.TryDispose();
            ev = EventPool.Instance.Rent();
            lt = DefaultRecyclablePool.Instance.Rent();
        }
    }

    private static void TestEventMechanics(Event ev, Recyclable lt)
    {
        var count = 0;;
        Action subscriber = () => count++;
        ev.Subscribe(subscriber, lt);
        ev.Fire();
        Assert.AreEqual(1, count);
        lt.Dispose();
        ev.Fire();
        Assert.AreEqual(1, count);
    }

    private static void TestEventMechanics(Event ev, Recyclable lt, object scope)
    {
        var count = 0; ;
        Action<object> subscriber = (sc) => count++;
        ev.Subscribe(scope, subscriber, lt);
        ev.Fire();
        Assert.AreEqual(1, count);
        lt.Dispose();
        ev.Fire();
        Assert.AreEqual(1, count);
    }

    private static void TestEventMechanicsT<T>(Event<T> ev, Recyclable lt, T args)
    {
        var count = 0; ;
        Action<T> subscriber = (sc) =>
        {
            Assert.AreEqual(args, sc);
            count++;
        };
        ev.Subscribe(subscriber, lt);
        ev.Fire(args);
        Assert.AreEqual(1, count);
        lt.Dispose();
        ev.Fire(args);
        Assert.AreEqual(1, count);
    }

    private static void TestEventMechanicsScoped<T>(Event<T> ev, Recyclable lt, T args, object scope)
    {
        var count = 0; ;
        Action<object,object> subscriber = (sc, ar) =>
        {
            Assert.AreEqual(scope, sc);
            Assert.AreEqual(args, ar);
            count++;
        };
        ev.Subscribe(scope, subscriber, lt);
        ev.Fire(args);
        Assert.AreEqual(1, count);
        lt.Dispose();
        ev.Fire(args);
        Assert.AreEqual(1, count);
    }

    #region Event (no generic argument) Tests

    [TestMethod]
    public void Event_FireWithNoSubscribers_NoEffect()
    {
        var ev = new Event();
        // No subscribers
        // Just ensure we don't throw
        ev.Fire();
        Assert.IsFalse(ev.HasSubscriptions);
    }

    [TestMethod]
    public void Event_Subscribe_ReceivesNotification()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        ev.Subscribe(() => callCount++, lt);
        ev.Fire();

        Assert.AreEqual(1, callCount, "Subscriber should have been called exactly once.");
    }

    [TestMethod]
    public void Event_SubscribeWithPriority_ReceivesNotification()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        ev.SubscribeWithPriority(() => callCount++, lt);
        ev.Fire();

        Assert.AreEqual(1, callCount, "Priority subscriber should also be called exactly once.");
    }

    [TestMethod]
    public void Event_SubscribeOnce_CalledOnlyOnce()
    {
        var ev = new Event();
        int callCount = 0;

        ev.SubscribeOnce(() => callCount++);
        Assert.IsTrue(ev.HasSubscriptions);

        ev.Fire();
        ev.Fire(); // second fire should not trigger anything

        Assert.AreEqual(1, callCount, "SubscribeOnce should only fire its callback once.");
        Assert.IsFalse(ev.HasSubscriptions, "After firing once, there should be no active subscriptions left.");
    }

    [TestMethod]
    public void Event_SubscribeWithScope_ScopeReceivesNotification()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        object myScope = new object();
        ev.Subscribe(myScope, scope => {
            Assert.AreSame(myScope, scope, "Scope object should match the one passed in.");
            callCount++;
        }, lt);

        ev.Fire();
        Assert.AreEqual(1, callCount);

        // Dispose lifetime, should not be called again
        lt.Dispose();
        ev.Fire();
        Assert.AreEqual(1, callCount, "After lifetime is disposed, callback should not be called again.");
    }

    [TestMethod]
    public void Event_SubscribeWithScopeAndPriority_ScopeReceivesNotification()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        object myScope = new object();
        ev.SubscribeWithPriority(myScope, scope => {
            Assert.AreSame(myScope, scope, "Scope object should match the one passed in.");
            callCount++;
        }, lt);

        ev.Fire();
        Assert.AreEqual(1, callCount);

        lt.Dispose();
        ev.Fire();
        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Event_Sync_CallsImmediatelyAndOnFire()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        ev.Sync(() => callCount++, lt);
        // Sync should have called immediately
        Assert.AreEqual(1, callCount, "Sync should call the callback immediately.");

        // Now fire event
        ev.Fire();
        Assert.AreEqual(2, callCount, "Sync subscription should also respond to Fire.");
    }

    [TestMethod]
    public void Event_SyncWithPriority_CallsImmediatelyAndOnFire()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        ev.SyncWithPriority(() => callCount++, lt);
        // Called immediately
        Assert.AreEqual(1, callCount);

        ev.Fire();
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Event_SyncWithScope_CallsImmediatelyAndOnFire()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;
        object myScope = new object();

        ev.Sync(myScope, scope => {
            Assert.AreSame(myScope, scope);
            callCount++;
        }, lt);

        // Immediately
        Assert.AreEqual(1, callCount);

        // Next Fire
        ev.Fire();
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Event_SyncWithPriorityAndScope_CallsImmediatelyAndOnFire()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;
        object myScope = new object();

        ev.SyncWithPriority(myScope, scope => {
            Assert.AreSame(myScope, scope);
            callCount++;
        }, lt);

        // Immediately
        Assert.AreEqual(1, callCount);

        // Next Fire
        ev.Fire();
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Event_SubscribeOnceWithScope_CalledOnceAndDisposed()
    {
        var ev = new Event();
        int callCount = 0;
        object myScope = new object();

        // SubscribeOnce overload that accepts scope 
        ev.SubscribeOnce(myScope, scope => {
            Assert.AreSame(myScope, scope);
            callCount++;
        });
        Assert.IsTrue(ev.HasSubscriptions);

        ev.Fire();
        ev.Fire(); // no effect second time
        Assert.AreEqual(1, callCount);
        Assert.IsFalse(ev.HasSubscriptions, "After one call, subscription should be removed.");
    }

    [TestMethod]
    public void Event_CreateNextFireLifetime_EndsAfterNextFire()
    {
        var ev = new Event();
        var lifetime = ev.CreateNextFireLifetime();
        Assert.IsFalse(lifetime.IsExpired);

        ev.Fire();
        Assert.IsTrue(lifetime.IsExpired, "Lifetime created should end after next fire.");
    }

    [TestMethod]
    public async Task Event_CreateNextFireTask_CompletesAfterNextFire()
    {
        var ev = new Event();
        var task = ev.CreateNextFireTask();

        Assert.IsFalse(task.IsCompleted, "Task should not be completed until Fire is called.");
        ev.Fire();

        await task; // should complete without exception
        Assert.IsTrue(task.IsCompleted, "Task should be completed after Fire.");
    }

    [TestMethod]
    public void Event_MultipleFires_NoSideEffectsIfLifetimeDisposed()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        ev.Subscribe(() => callCount++, lt);
        ev.Fire();
        ev.Fire();
        Assert.AreEqual(2, callCount);

        lt.Dispose();
        ev.Fire();
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void Event_PooledReusability_NoLeakBetweenUsages()
    {
        for (int i = 0; i < 5; i++)
        {
            var ev = EventPool.Instance.Rent();
            var lt = DefaultRecyclablePool.Instance.Rent();
            int callCount = 0;

            ev.Subscribe(() => callCount++, lt);
            ev.Fire();
            Assert.AreEqual(1, callCount);
            ev.TryDispose();
            lt.TryDispose();

            // Next iteration: new subscription
            // If there's a leak, callCount might get incremented incorrectly.
        }
    }

    [TestMethod]
    public void Event_Reentrancy_FiringInsideCallback()
    {
        var ev = new Event();
        var lt = new Recyclable();
        int callCount = 0;

        ev.Subscribe(() =>
        {
            callCount++;
            // Fire again inside callback
            if (callCount == 1) ev.Fire();
        }, lt);

        ev.Fire();
        // In the first callback call, we re-fired, so we should have 2 total calls
        Assert.AreEqual(2, callCount);
    }

    #endregion

    #region Event<T> Tests

    [TestMethod]
    public void EventT_FireWithNoSubscribers_NoEffect()
    {
        var ev = new Event<int>();
        ev.Fire(42); // no subscribers
                     // Just ensure no exceptions
    }

    [TestMethod]
    public void EventT_Subscribe_CalledWithCorrectArgument()
    {
        var ev = new Event<string>();
        var lt = new Recyclable();
        string lastMessage = null;

        ev.Subscribe(arg => lastMessage = arg, lt);
        ev.Fire("Hello");

        Assert.AreEqual("Hello", lastMessage);
    }

    [TestMethod]
    public void EventT_SubscribeWithPriority_CalledCorrectly()
    {
        var ev = new Event<int>();
        var lt = new Recyclable();
        int lastValue = 0;

        ev.SubscribeWithPriority(arg => lastValue = arg, lt);
        ev.Fire(5);

        Assert.AreEqual(5, lastValue);
    }

    [TestMethod]
    public void EventT_SubscribeOnce_CalledOnce()
    {
        var ev = new Event<int>();
        int callCount = 0;

        ev.SubscribeOnce(arg => callCount++);
        ev.Fire(10);
        ev.Fire(11);

        Assert.AreEqual(1, callCount, "SubscribeOnce for Event<T> should only be called once.");
    }

    [TestMethod]
    public void EventT_SubscribeWithScope_ReceivesScopeAndArgument()
    {
        var ev = new Event<int>();
        var lt = new Recyclable();
        object scope = new object();
        int callCount = 0;

        ev.Subscribe(scope, (sc, arg) =>
        {
            Assert.AreSame(scope, sc);
            Assert.AreEqual(123, (int)arg);
            callCount++;
        }, lt);

        ev.Fire(123);
        Assert.AreEqual(1, callCount);

        lt.Dispose();
        ev.Fire(456);
        Assert.AreEqual(1, callCount, "Disposed lifetime should not receive further notifications.");
    }

    [TestMethod]
    public void EventT_SubscribeWithPriorityScope_ReceivesScopeAndArgument()
    {
        var ev = new Event<int>();
        var lt = new Recyclable();
        object scope = new object();
        int callCount = 0;

        ev.SubscribeWithPriority(scope, (s, arg) =>
        {
            Assert.AreSame(scope, s);
            Assert.AreEqual(123, arg);
            callCount++;
        }, lt);

        ev.Fire(123);
        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void EventT_SubscribeOnceWithScope_CalledOnce()
    {
        var ev = new Event<int>();
        object scope = new object();
        int callCount = 0;

        ev.SubscribeOnce(scope, (sc, arg) =>
        {
            Assert.AreSame(scope, sc);
            Assert.AreEqual(42, arg);
            callCount++;
        });

        ev.Fire(42);
        ev.Fire(42);
        Assert.AreEqual(1, callCount, "SubscribeOnce with scope for Event<T> should only be called once.");
    }

    [TestMethod]
    public void EventT_CreateNextFireLifetime_EndsAfterNextFire()
    {
        var ev = new Event<int>();
        var lifetime = ev.CreateNextFireLifetime();
        Assert.IsFalse(lifetime.IsExpired);

        ev.Fire(100);
        Assert.IsTrue(lifetime.IsExpired, "Lifetime should end on next Fire.");
    }

    [TestMethod]
    public async Task EventT_CreateNextFireTask_CompletesAfterNextFire()
    {
        var ev = new Event<int>();
        var task = ev.CreateNextFireTask();

        Assert.IsFalse(task.IsCompleted, "Should not complete before Fire.");
        ev.Fire(999);

        var result = await task;
        Assert.AreEqual(999, result, "Task should have completed with the last Fire argument.");
    }

    [TestMethod]
    public void EventT_MultipleFiresAndDispose_NoSideEffects()
    {
        var ev = new Event<string>();
        var lt = new Recyclable();
        int callCount = 0;

        ev.Subscribe(_ => callCount++, lt);
        ev.Fire("A");
        ev.Fire("B");
        Assert.AreEqual(2, callCount);

        lt.Dispose();
        ev.Fire("C");
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public void EventT_PooledReusability_NoLeaksBetweenUsages()
    {
        for (int i = 0; i < 5; i++)
        {
            var ev = EventPool<string>.Instance.Rent();
            var lt = DefaultRecyclablePool.Instance.Rent();
            int callCount = 0;

            ev.Subscribe(_ => callCount++, lt);
            ev.Fire("X");
            Assert.AreEqual(1, callCount);
            ev.TryDispose();
            lt.TryDispose();

            // If there's a memory leak or leftover subscription from prior usage,
            // subsequent iterations might incorrectly increment callCount.
        }
    }

    [TestMethod]
    public void EventT_Reentrancy_FiringInsideCallback()
    {
        var ev = new Event<int>();
        var lt = new Recyclable();
        int callCount = 0;

        ev.Subscribe(arg =>
        {
            callCount++;
            if (callCount == 1)
            {
                ev.Fire(arg + 1); // re-fire
            }
        }, lt);

        ev.Fire(10);
        // first call increments to 1, triggers re-fire, second call increments to 2
        Assert.AreEqual(2, callCount);
    }

    #endregion
}
 
