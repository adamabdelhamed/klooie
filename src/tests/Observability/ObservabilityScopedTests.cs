using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests
{
    [TestClass]
    [TestCategory(Categories.Observability)]
    public partial class ObservabilityScopedTests
    {
        public partial class SomeObservable2 : Recyclable, IObservableObject
        {
            public Event SomeEvent { get; } = Event.Create();
            public Event<string> SomeEventWithAString { get; } = Event<string>.Create();


            public partial string Name { get; set; }

            public partial int Number { get; set; }
        }

        // A simple "scope object" we can pass around without capturing local variables.
        private class MyScope
        {
            public int FiredCount;
        }

        // Static handler (no captures). For Event (no generic argument).
        private static void OnEventFired(object scope)
        {
            ((MyScope)scope).FiredCount++;
        }

        // Static handler (no captures). For Event<T>.
        private static void OnStringEventFired(object scope, object str)
        {
            ((MyScope)scope).FiredCount++;
        }

        /// <summary>
        /// Test Subscribe(...) with scope for a lifetime-based subscription to Event.
        /// </summary>
        [TestMethod]
        public void SubscribeForLifetimeToEventWithScope()
        {
            var observable = new SomeObservable2();
            var scope = new MyScope();

            var lifetime = DefaultRecyclablePool.Instance.Rent();
            try
            { 
                // Use the scope-based Subscribe overload
                observable.SomeEvent.Subscribe(scope, OnEventFired, lifetime);

                Assert.AreEqual(0, scope.FiredCount);
                observable.SomeEvent.Fire();
                Assert.AreEqual(1, scope.FiredCount);

                observable.SomeEvent.Fire();
                Assert.AreEqual(2, scope.FiredCount);
            }
            finally
            {
                lifetime.Dispose();
            }
            // After lifetime is disposed, no more firing
            observable.SomeEvent.Fire();
            Assert.AreEqual(2, scope.FiredCount);
        }

        /// <summary>
        /// Test SubscribeOnce(...) with scope for Event.
        /// </summary>
        [TestMethod]
        public void SubscribeOnceToEventWithScope()
        {
            var observable = new SomeObservable2();
            var scope = new MyScope();

            // There's no lifetime needed in SubscribeOnce(...)
            observable.SomeEvent.SubscribeOnce(scope, OnEventFired);

            Assert.AreEqual(0, scope.FiredCount);
            observable.SomeEvent.Fire();
            Assert.AreEqual(1, scope.FiredCount);

            // Fire again; the subscription should have been removed
            observable.SomeEvent.Fire();
            Assert.AreEqual(1, scope.FiredCount);
        }

        /// <summary>
        /// Test SubscribeWithPriority(...) with scope for Event.
        /// </summary>
        [TestMethod]
        public void SubscribeWithPriorityToEventWithScope()
        {
            var observable = new SomeObservable2();
            var scope = new MyScope();

            var lifetime = DefaultRecyclablePool.Instance.Rent();
            try
            {
                // Just like normal, but we'll call the priority-based method
                observable.SomeEvent.SubscribeWithPriority(scope, OnEventFired, lifetime);

                Assert.AreEqual(0, scope.FiredCount);
                observable.SomeEvent.Fire();
                Assert.AreEqual(1, scope.FiredCount);
            }
            finally
            {
                lifetime.Dispose();
            }
            // After disposing
            observable.SomeEvent.Fire();
            Assert.AreEqual(1, scope.FiredCount);
        }

        /// <summary>
        /// Test Subscribe(...) with scope for Event<string>.
        /// We verify it increments when the event fires with a string.
        /// </summary>
        [TestMethod]
        public void SubscribeForLifetimeToEventOfStringWithScope()
        {
            var observable = new SomeObservable2();
            var scope = new MyScope();


            var lifetime = DefaultRecyclablePool.Instance.Rent();
            try
            {
                observable.SomeEventWithAString.Subscribe(scope, OnStringEventFired, lifetime);

                Assert.AreEqual(0, scope.FiredCount);

                // Fire with some string argument
                observable.SomeEventWithAString.Fire("hello");
                Assert.AreEqual(1, scope.FiredCount);

                observable.SomeEventWithAString.Fire("world");
                Assert.AreEqual(2, scope.FiredCount);
            }
            finally
            {
                lifetime.Dispose();
            }
            // Once lifetime is disposed, no more increments
            observable.SomeEventWithAString.Fire("ignored");
            Assert.AreEqual(2, scope.FiredCount);
        }

        /// <summary>
        /// Test SubscribeOnce(...) with scope for Event<string>.
        /// </summary>
        [TestMethod]
        public void SubscribeOnceToEventOfStringWithScope()
        {
            var observable = new SomeObservable2();
            var scope = new MyScope();

            observable.SomeEventWithAString.SubscribeOnce(scope, OnStringEventFired);

            Assert.AreEqual(0, scope.FiredCount);
            observable.SomeEventWithAString.Fire("first");
            Assert.AreEqual(1, scope.FiredCount);

            // Fire again, should no longer increment
            observable.SomeEventWithAString.Fire("second");
            Assert.AreEqual(1, scope.FiredCount);
        }
    }
}
