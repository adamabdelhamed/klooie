using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace klooie.tests;
[TestClass]
[TestCategory(Categories.Observability)]
public class EventTests
{
    [TestMethod]
    public void TestEvent()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            TestEventMechanics(ev, lt);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void TestEventT()
    {
        var ev = EventPool<object>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            TestEventMechanicsT(ev, lt, new object());
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void TestEventTScoped()
    {
        object scope = new object();
        int args = 1;
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            TestEventMechanicsScoped(ev, lt, args, scope);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void TestEventScoped()
    {
        object scope = new object();
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            TestEventMechanics(ev, lt, scope);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void TestEventReusability()
    {
        for (var i = 0; i < 10; i++)
        {
            var ev = EventPool.Instance.Rent();
            var lt = DefaultRecyclablePool.Instance.Rent();
            try
            {
                TestEventMechanics(ev, lt);
            }
            finally
            {
                ev.TryDispose();
                lt.TryDispose();
            }
        }
    }

    [TestMethod]
    public void TestEventReusabilityScopedAndUnscoped()
    {
        var scope = new object();
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
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
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    private static void TestEventMechanics(Event ev, Recyclable lt)
    {
        var count = 0;
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
        var count = 0;
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
        var count = 0;
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
        var count = 0;
        Action<object, object> subscriber = (sc, ar) =>
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
        var ev = EventPool.Instance.Rent();
        try
        {
            ev.Fire();
            Assert.IsFalse(ev.HasSubscriptions);
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void Event_Subscribe_ReceivesNotification()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.Subscribe(() => callCount++, lt);
            ev.Fire();

            Assert.AreEqual(1, callCount, "Subscriber should have been called exactly once.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SubscribeWithPriority_ReceivesNotification()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeWithPriority(() => callCount++, lt);
            ev.Fire();

            Assert.AreEqual(1, callCount, "Priority subscriber should also be called exactly once.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SubscribeOnce_CalledOnlyOnce()
    {
        var ev = EventPool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeOnce(() => callCount++);
            Assert.IsTrue(ev.HasSubscriptions);

            ev.Fire();
            ev.Fire(); // second fire should not trigger anything

            Assert.AreEqual(1, callCount, "SubscribeOnce should only fire its callback once.");
            Assert.IsFalse(ev.HasSubscriptions, "After firing once, there should be no active subscriptions left.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SubscribeWithScope_ScopeReceivesNotification()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            object myScope = new object();
            ev.Subscribe(myScope, scope =>
            {
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
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SubscribeWithScopeAndPriority_ScopeReceivesNotification()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            object myScope = new object();
            ev.SubscribeWithPriority(myScope, scope =>
            {
                Assert.AreSame(myScope, scope, "Scope object should match the one passed in.");
                callCount++;
            }, lt);

            ev.Fire();
            Assert.AreEqual(1, callCount);

            lt.Dispose();
            ev.Fire();
            Assert.AreEqual(1, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_Sync_CallsImmediatelyAndOnFire()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.Sync(() => callCount++, lt);
            Assert.AreEqual(1, callCount, "Sync should call the callback immediately.");

            ev.Fire();
            Assert.AreEqual(2, callCount, "Sync subscription should also respond to Fire.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SyncWithPriority_CallsImmediatelyAndOnFire()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SyncWithPriority(() => callCount++, lt);
            Assert.AreEqual(1, callCount);

            ev.Fire();
            Assert.AreEqual(2, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SyncWithScope_CallsImmediatelyAndOnFire()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;
            object myScope = new object();

            ev.Sync(myScope, scope =>
            {
                Assert.AreSame(myScope, scope);
                callCount++;
            }, lt);

            Assert.AreEqual(1, callCount);

            ev.Fire();
            Assert.AreEqual(2, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SyncWithPriorityAndScope_CallsImmediatelyAndOnFire()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;
            object myScope = new object();

            ev.SyncWithPriority(myScope, scope =>
            {
                Assert.AreSame(myScope, scope);
                callCount++;
            }, lt);

            Assert.AreEqual(1, callCount);

            ev.Fire();
            Assert.AreEqual(2, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_SubscribeOnceWithScope_CalledOnceAndDisposed()
    {
        var ev = EventPool.Instance.Rent();
        try
        {
            int callCount = 0;
            object myScope = new object();

            ev.SubscribeOnce(myScope, scope =>
            {
                Assert.AreSame(myScope, scope);
                callCount++;
            });
            Assert.IsTrue(ev.HasSubscriptions);

            ev.Fire();
            ev.Fire();  // no effect second time
            Assert.AreEqual(1, callCount);
            Assert.IsFalse(ev.HasSubscriptions, "After one call, subscription should be removed.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void Event_CreateNextFireLifetime_EndsAfterNextFire()
    {
        var ev = EventPool.Instance.Rent();
        try
        {
            var lifetime = ev.CreateNextFireLifetime();
            var lease = lifetime.Lease;
            Assert.IsTrue(lifetime.IsStillValid(lease));
            ev.Fire();
            Assert.IsFalse(lifetime.IsStillValid(lease), "Lifetime created should end after next fire.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public async Task Event_CreateNextFireTask_CompletesAfterNextFire()
    {
        var ev = EventPool.Instance.Rent();
        try
        {
            var task = ev.CreateNextFireTask();

            Assert.IsFalse(task.IsCompleted, "Task should not be completed until Fire is called.");
            ev.Fire();

            await task;
            Assert.IsTrue(task.IsCompleted, "Task should be completed after Fire.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void Event_MultipleFires_NoSideEffectsIfLifetimeDisposed()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.Subscribe(() => callCount++, lt);
            ev.Fire();
            ev.Fire();
            Assert.AreEqual(2, callCount);

            lt.Dispose();
            ev.Fire();
            Assert.AreEqual(2, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void Event_PooledReusability_NoLeakBetweenUsages()
    {
        for (int i = 0; i < 5; i++)
        {
            var ev = EventPool.Instance.Rent();
            var lt = DefaultRecyclablePool.Instance.Rent();
            try
            {
                int callCount = 0;
                ev.Subscribe(() => callCount++, lt);
                ev.Fire();
                Assert.AreEqual(1, callCount);
            }
            finally
            {
                ev.TryDispose();
                lt.TryDispose();
            }

            // Next iteration: new subscription
            // If there's a leak, callCount might get incremented incorrectly.
        }
    }

    [TestMethod]
    public void Event_Reentrancy_FiringInsideCallback()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
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
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    #endregion

    #region Event<T> Tests

    [TestMethod]
    public void EventT_FireWithNoSubscribers_NoEffect()
    {
        var ev = EventPool<int>.Instance.Rent();
        try
        {
            ev.Fire(42);
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_Subscribe_CalledWithCorrectArgument()
    {
        var ev = EventPool<string>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            string lastMessage = null;

            ev.Subscribe(arg => lastMessage = arg, lt);
            ev.Fire("Hello");

            Assert.AreEqual("Hello", lastMessage);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_SubscribeWithPriority_CalledCorrectly()
    {
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int lastValue = 0;

            ev.SubscribeWithPriority(arg => lastValue = arg, lt);
            ev.Fire(5);

            Assert.AreEqual(5, lastValue);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_SubscribeOnce_CalledOnce()
    {
        var ev = EventPool<int>.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeOnce(arg => callCount++);
            ev.Fire(10);
            ev.Fire(11);

            Assert.AreEqual(1, callCount, "SubscribeOnce for Event<T> should only be called once.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_SubscribeWithScope_ReceivesScopeAndArgument()
    {
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
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
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_SubscribeWithPriorityScope_ReceivesScopeAndArgument()
    {
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
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
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_SubscribeOnceWithScope_CalledOnce()
    {
        var ev = EventPool<int>.Instance.Rent();
        try
        {
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
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_CreateNextFireLifetime_EndsAfterNextFire()
    {
        var ev = EventPool<int>.Instance.Rent();
        try
        {
            var lifetime = ev.CreateNextFireLifetime();
            var ltLease = lifetime.Lease;
            Assert.IsTrue(lifetime.IsStillValid(ltLease));
            ev.Fire(100);
            Assert.IsFalse(lifetime.IsStillValid(ltLease), "Lifetime should end on next Fire.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public async Task EventT_CreateNextFireTask_CompletesAfterNextFire()
    {
        var ev = EventPool<int>.Instance.Rent();
        try
        {
            var task = ev.CreateNextFireTask();

            Assert.IsFalse(task.IsCompleted, "Should not complete before Fire.");
            ev.Fire(999);

            var result = await task;
            Assert.AreEqual(999, result, "Task should have completed with the last Fire argument.");
        }
        finally
        {
            ev.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_MultipleFiresAndDispose_NoSideEffects()
    {
        var ev = EventPool<string>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.Subscribe(_ => callCount++, lt);
            ev.Fire("A");
            ev.Fire("B");
            Assert.AreEqual(2, callCount);

            lt.Dispose();
            ev.Fire("C");
            Assert.AreEqual(2, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventT_PooledReusability_NoLeaksBetweenUsages()
    {
        for (int i = 0; i < 5; i++)
        {
            var ev = EventPool<string>.Instance.Rent();
            var lt = DefaultRecyclablePool.Instance.Rent();
            try
            {
                int callCount = 0;
                ev.Subscribe(_ => callCount++, lt);
                ev.Fire("X");
                Assert.AreEqual(1, callCount);
            }
            finally
            {
                ev.TryDispose();
                lt.TryDispose();
            }
        }
    }

    [TestMethod]
    public void EventT_Reentrancy_FiringInsideCallback()
    {
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.Subscribe(arg =>
            {
                callCount++;
                if (callCount == 1)
                {
                    ev.Fire(arg + 1);
                }
            }, lt);

            ev.Fire(10);
            Assert.AreEqual(2, callCount);
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    #endregion

    #region Throttling Tests

    [TestMethod]
    public void EventThrottle_Unscoped_BasicRateLimit()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;
            ev.SubscribeThrottled(() => callCount++, lt, maxHz: 10);

            for (int i = 0; i < 5; i++) ev.Fire();

            Assert.AreEqual(1, callCount, "Throttle should allow at most one event in the window.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public async Task EventThrottle_Unscoped_AllowsAfterWindow()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeThrottled(() => callCount++, lt, maxHz: 10);
            ev.Fire();
            await Task.Delay(120);
            ev.Fire();

            Assert.AreEqual(2, callCount, "Second event after window should be delivered.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventThrottle_Scoped_BasicRateLimit()
    {
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            object scope = new object();
            int callCount = 0;

            ev.SubscribeThrottled(scope, (sc, i) =>
            {
                Assert.AreSame(scope, sc);
                callCount++;
            }, lt, maxCyclesPerSecond: 5);

            for (int i = 0; i < 3; i++) ev.Fire(123);

            Assert.AreEqual(1, callCount, "Scoped throttle should limit to one call per window.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventThrottle_Lifetime_DisposeStopsCallbacks()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeThrottled(() => callCount++, lt, maxHz: 50);
            ev.Fire();
            lt.Dispose();
            ev.Fire();

            Assert.AreEqual(1, callCount, "Disposing lifetime should stop further callbacks.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventThrottle_ConcurrentFires_NoMoreThanOnePerWindow()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeThrottled(() => Interlocked.Increment(ref callCount), lt, maxHz: 5);

            Parallel.For(0, 20, _ => ev.Fire());

            Assert.AreEqual(1, callCount, "Even under concurrency, only one invocation should occur within the window.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public async Task EventThrottle_RepeatedWindows_CorrectCadence()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;

            ev.SubscribeThrottled(() => callCount++, lt, maxHz: 4);

            for (int i = 0; i < 4; i++)
            {
                ev.Fire();
                await Task.Delay(300);
            }

            Assert.AreEqual(4, callCount, "One invocation should pass per window over multiple windows.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventThrottle_Scoped_Untyped_PassesScopeAndThrottles()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            object scope = new object();
            int callCount = 0;

            ev.SubscribeThrottled(scope, sc =>
            {
                Assert.AreSame(scope, sc);
                callCount++;
            }, lt, maxCyclesPerSecond: 10);

            for (int i = 0; i < 5; i++) ev.Fire();
            Assert.AreEqual(1, callCount, "Non-generic scoped throttle should limit to one call per window and pass correct scope.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public async Task EventThrottle_Scoped_Untyped_SecondFireAfterWindow()
    {
        var ev = EventPool.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            object scope = new object();
            int callCount = 0;

            ev.SubscribeThrottled(scope, sc =>
            {
                Assert.AreSame(scope, sc);
                callCount++;
            }, lt, maxCyclesPerSecond: 10);

            ev.Fire();
            await Task.Delay(120);
            ev.Fire();

            Assert.AreEqual(2, callCount, "Non-generic scoped throttle should allow calls after throttle window.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventThrottle_Scoped_Generic_PassesScopeAndArg()
    {
        var ev = EventPool<string>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            object scope = new object();
            int callCount = 0;
            string lastArg = null;

            ev.SubscribeThrottled(scope, (sc, arg) =>
            {
                Assert.AreSame(scope, sc);
                lastArg = arg;
                callCount++;
            }, lt, maxCyclesPerSecond: 20);

            ev.Fire("alpha");
            for (int i = 0; i < 4; i++) ev.Fire("beta");

            Assert.AreEqual(1, callCount, "Generic scoped throttle should only allow one call per window.");
            Assert.AreEqual("alpha", lastArg, "First argument should be delivered, not overwritten.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public void EventThrottle_GenericUnscoped_BasicRateLimit()
    {
        var ev = EventPool<int>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;
            int lastValue = 0;

            ev.SubscribeThrottled(i => { callCount++; lastValue = i; }, lt, maxCyclesPerSecond: 10);

            ev.Fire(5);
            ev.Fire(6);
            ev.Fire(7);

            Assert.AreEqual(1, callCount, "Generic unscoped throttle should allow at most one event per window.");
            Assert.AreEqual(5, lastValue, "First event's payload should be delivered.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }

    [TestMethod]
    public async Task EventThrottle_GenericUnscoped_SecondFireAfterWindow()
    {
        var ev = EventPool<string>.Instance.Rent();
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            int callCount = 0;
            string lastValue = null;

            ev.SubscribeThrottled(val => { callCount++; lastValue = val; }, lt, maxCyclesPerSecond: 4);

            ev.Fire("a");
            await Task.Delay(260);
            ev.Fire("b");

            Assert.AreEqual(2, callCount, "Should get a callback for each event outside the throttle window.");
            Assert.AreEqual("b", lastValue, "Payload of second event should be delivered after the window.");
        }
        finally
        {
            ev.TryDispose();
            lt.TryDispose();
        }
    }
    #endregion
}
