using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Reflection;
using System.Collections.ObjectModel;

namespace PowerArgs
{
    public interface IObservableObject
    {
        bool SuppressEqualChanges { get; set; }
        void SubscribeForLifetime(string propertyName, Action handler, ILifetimeManager lifetimeManager);
        void SynchronizeForLifetime(string propertyName, Action handler, ILifetimeManager lifetimeManager);
        object GetPrevious(string propertyName);

        T Get<T>(string name);
        void Set<T>(T value, string name);

        ILifetimeManager GetPropertyValueLifetime(string propertyName);

    }

    /// <summary>
    /// A class that makes it easy to define an object with observable properties
    /// </summary>
    public class ObservableObject : Lifetime, IObservableObject
    {
        /// <summary>
        /// Subscribe or synchronize using this key to receive notifications when any property changes
        /// </summary>
        public const string AnyProperty = "*";

        private Dictionary<string, Event> subscribers;
        private Dictionary<string, object> values;
        private Dictionary<string, object> previousValues;

        /// <summary>
        /// Set to true if you want to suppress notification events for properties that get set to their existing values.
        /// </summary>
        public bool SuppressEqualChanges { get; set; }

        /// <summary>
        /// DeepObservableRoot
        /// </summary>
        public IObservableObject DeepObservableRoot { get; private set; }

        public IReadOnlyDictionary<string, object> ToDictionary() => values != null ? new ReadOnlyDictionary<string, object>(values) : new Dictionary<string, object>();

        public string CurrentlyChangingPropertyName { get; private set; }

        /// <summary>
        /// Creates a new bag and optionally sets the notifier object.
        /// </summary>
        public ObservableObject(IObservableObject proxy = null)
        {
            SuppressEqualChanges = true;
            DeepObservableRoot = proxy;
        }

        /// <summary>
        /// returns true if this object has a property with the given key
        /// </summary>
        /// <param name="key">the property name</param>
        /// <returns>true if this object has a property with the given key</returns>
        public bool ContainsKey(string key) => values != null && values.ContainsKey(key);


        /// <summary>
        /// returns true if this object has a property with the given key and val was populated
        /// </summary>
        /// <typeparam name="T">the type of property to get</typeparam>
        /// <param name="key">the name of the property</param>
        /// <param name="val">the output value</param>
        /// <returns>true if this object has a property with the given key and val was populated</returns>
        public bool TryGetValue<T>(string key, out T val)
        {
            if(values == null)
            {
                val = default;
                return false;
            }
            if (values.TryGetValue(key, out object oVal))
            {
                val = (T)oVal;
                return true;
            }
            else
            {
                val = default(T);
                return false;
            }
        }


        /// <summary>
        /// This should be called by a property getter to get the value
        /// </summary>
        /// <typeparam name="T">The type of property to get</typeparam>
        /// <param name="name">The name of the property to get</param>
        /// <returns>The property's current value</returns>
        public T Get<T>([CallerMemberName] string name = "")
        {
            values = values ?? new Dictionary<string, object>();
            previousValues = previousValues ?? new Dictionary<string, object>();
            object ret;
            if (values.TryGetValue(name, out ret))
            {
                if(ret == null)
                {
                    return default(T);
                }
                else if (ret is T)
                {
                    return (T)ret;
                }
                else
                {
                    return (T)Convert.ChangeType(ret, typeof(T));
                }
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// Gets the previous value of the given property name
        /// </summary>
        /// <typeparam name="T">the type of property to get</typeparam>
        /// <param name="name">the name of the property</param>
        /// <returns>the previous value or default(T) if there was none</returns>
        public T GetPrevious<T>([CallerMemberName] string name = "")
        {
            object ret;
            if (previousValues.TryGetValue(name, out ret))
            {
                return (T)ret;
            }
            else
            {
                return default(T);
            }
        }

        object IObservableObject.GetPrevious(string name) => this.GetPrevious<object>(name);

        /// <summary>
        /// This should be called by a property getter to set the value.
        /// </summary>
        /// <typeparam name="T">The type of property to set</typeparam>
        /// <param name="value">The value to set</param>
        /// <param name="name">The name of the property to set</param>
        public void Set<T>(T value, [CallerMemberName] string name = "")
        {
            var current = Get<object>(name);
            var isEqualChange = EqualsSafe(current, value);

            if (values.ContainsKey(name))
            {
                if (SuppressEqualChanges == false || isEqualChange == false)
                {
                    previousValues[name] = current;
                }
                values[name] = value;
            }
            else
            {
                values.Add(name, value);
            }

            if (SuppressEqualChanges == false || isEqualChange == false)
            {
                CurrentlyChangingPropertyName = name;
                FirePropertyChanged(name);
            }
        }

        public void Set<T>(ref T current, T value, [CallerMemberName] string name = "")
        {
            var isEqualChange = EqualsSafe(current, value);

            if (SuppressEqualChanges == false || isEqualChange == false)
            {
                previousValues[name] = current;
            }

            current = value;

            if (SuppressEqualChanges == false || isEqualChange == false)
            {
                CurrentlyChangingPropertyName = name;
                FirePropertyChanged(name);
            }
        }

        public void SetHardIf<T>(ref T current, T value, bool condition, [CallerMemberName] string name = "")
        {
            if (condition == false) return;
            current = value;
            CurrentlyChangingPropertyName = name;
            FirePropertyChanged(name);
        }

        private Event GetEvent(string propertyName)
        {
            subscribers = subscribers ?? new Dictionary<string, Event>();
            Event evForProperty;
            if (subscribers.TryGetValue(propertyName, out evForProperty) == false)
            {
                evForProperty = new Event();
                subscribers.Add(propertyName, evForProperty);
            }
            return evForProperty;
        }

        /// <summary>
        /// Subscribes to be notified when the given property changes.  The subscription expires when
        /// the given lifetime manager's lifetime ends.
        /// </summary>
        /// <param name="propertyName">The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be notified of any property change.</param>
        /// <param name="handler">The action to call for notifications</param>
        /// <param name="lifetimeManager">the lifetime manager that determines when the subscription ends</param>
        public void SubscribeForLifetime(string propertyName, Action handler, ILifetimeManager lifetimeManager)
        {
            GetEvent(propertyName).SubscribeForLifetime(handler, lifetimeManager);
        }

        /// <summary>
        ///  Subscribes to be notified once when the given property changes.   
        /// </summary>
        /// <param name="propertyName">The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be notified of any property change.</param>
        /// <param name="handler">The action to call for notifications</param>
        public void SubscribeOnce(string propertyName, Action handler)
        {
            GetEvent(propertyName).SubscribeOnce(handler);
        }

        /// <summary>
        ///  Subscribes to be notified once when the given property changes.   
        /// </summary>
        /// <param name="propertyName">The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be notified of any property change.</param>
        /// <param name="toCleanup">The disposable to cleanup the next time the property changes</param>
        public void SubscribeOnce(string propertyName, IDisposable toCleanup) => SubscribeOnce(propertyName, toCleanup.Dispose);

        /// <summary>
        /// Subscribes to be notified when the given property changes and also fires an initial notification.  The subscription expires when
        /// the given lifetime manager's lifetime ends.
        /// </summary>
        /// <param name="propertyName">The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be notified of any property change.</param>
        /// <param name="handler">The action to call for notifications</param>
        /// <param name="lifetimeManager">the lifetime manager that determines when the subscription ends</param>

        public void SynchronizeForLifetime(string propertyName, Action handler, ILifetimeManager lifetimeManager)
        {
            GetEvent(propertyName).SynchronizeForLifetime(handler, lifetimeManager);
        }

        public ILifetimeManager GetPropertyValueLifetime(string propertyName)
        {
            var lt = new Lifetime();
            GetEvent(propertyName).SubscribeOnce(lt.Dispose);
            return lt.Manager;
        }

        /// <summary>
        /// Fires the PropertyChanged event with the given property name.
        /// </summary>
        /// <param name="propertyName">the name of the property that changed</param>
        public void FirePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
            if (subscribers == null) return;
            if (subscribers.TryGetValue(propertyName, out Event ev))
            {
                ev.Fire();
            }

            if (subscribers.TryGetValue(AnyProperty, out Event ev2))
            {
                ev2.Fire();
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {

        }

        /// <summary>
        /// A generic equals implementation that allows nulls to be passed for either parameter.  Objects should not call this from
        /// within their own equals method since that will cause a stack overflow.  The Equals() functions do not get called if the two
        /// inputs reference the same object.
        /// </summary>
        /// <param name="a">The first object to test</param>
        /// <param name="b">The second object to test</param>
        /// <returns>True if the values are equal, false otherwise.</returns>
        public static bool EqualsSafe(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null ^ b == null) return false;
            if (object.ReferenceEquals(a, b)) return true;

            return a.Equals(b);
        }

    }
}
