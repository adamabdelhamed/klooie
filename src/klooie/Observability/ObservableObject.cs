using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace klooie;

/// <summary>
/// interface for an object with observable properties
/// </summary>
public interface IObservableObject
{
    void Subscribe(string propertyName, Action handler, ILifetimeManager lifetimeManager);
    void Sync(string propertyName, Action handler, ILifetimeManager lifetimeManager);

    ILifetimeManager GetPropertyValueLifetime(string propertyName);
    /*
        // If you have a type that derives from a base that is not an ObservableObject
        // then you can implement IObservableObject and paste in the body of this sample
        // class. Your type will then be observable.

        public class ObservableAdapter : IObservableObject
        {
            private ObservableObject observable = new ObservableObject();
            public void Subscribe(string p, Action h, ILifetimeManager l) => observable.Subscribe(p, h, l);
            public void Sync(string p, Action h, ILifetimeManager l) => observable.Sync(p, h, l);
            public object GetPrevious(string p) => observable.GetPrevious<object>(p);
            public T Get<T>(string name) => observable.Get<T>(name);
            public void Set<T>(T value, string name) => observable.Set(value, name);
            public ILifetimeManager GetPropertyValueLifetime(string p) => observable.GetPropertyValueLifetime(p);
        }
     */
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

    /// <summary>
    /// Converts this object into a dictionary
    /// </summary>
    /// <returns>a dictionary</returns>
    public IDictionary<string, object> ToDictionary() => values != null ? new Dictionary<string, object>(values) : new Dictionary<string, object>();


    /// <summary>
    /// Converts this object into a read only dictionary
    /// </summary>
    /// <returns>a read only dictionary</returns>
    public IReadOnlyDictionary<string, object> ToReadOnlyDictionary() => values != null ? new ReadOnlyDictionary<string, object>(values) : new Dictionary<string, object>();


    /// <summary>
    /// returns true if this object has a property with the given key
    /// </summary>
    /// <param name="key">the property name</param>
    /// <returns>true if this object has a property with the given key</returns>
    public bool ContainsKey(string key) => values != null && values.ContainsKey(key);

    /// <summary>
    /// This should be called by a property getter to get the value
    /// </summary>
    /// <typeparam name="T">The type of property to get</typeparam>
    /// <param name="name">The name of the property to get</param>
    /// <returns>The property's current value</returns>
    public T Get<T>([CallerMemberName] string name = "") => TryGetValue(name, out T val) ? val : val;

    /// <summary>
    /// tries to get the value for the key provided
    /// </summary>
    /// <typeparam name="T">the type of value to get</typeparam>
    /// <param name="key">the key</param>
    /// <param name="val">the value to be populated or default(T)</param>
    /// <returns>true if the value was retrieved, false otherwise</returns>
    public bool TryGetValue<T>(string key, out T val)
    {
        values = values ?? new Dictionary<string, object>();
        object ret;
        if (values.TryGetValue(key, out ret))
        {
            if (ret == null)
            {
                val= default;
            }
            else if (ret is T)
            {
                val = (T)ret;
            }
            else
            {
                val = (T)Convert.ChangeType(ret, typeof(T));
            }
            return true;
        }
        else
        {
            val = default;
            return false;
        }
    }
 

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
            values[name] = value;
        }
        else
        {
            values.Add(name, value);
        }

        if (isEqualChange == false)
        {
            FirePropertyChanged(name);
        }
    }

    /// <summary>
    /// This method is useful for performance critical scenario, but has side effects. It allows the owning
    /// type to declare fields for observability rather than depending on the dictionary
    /// that this type uses to store values. It is much faster, but makes the owning type's
    /// code more complex. It also means that you can't ever call Get() or TryGetValue()
    /// since they will never have your value stored. And if you were to enumerate over
    /// this object's dictionary to find all property names then any properties set by
    /// this method will not appear.
    /// </summary>
    /// <typeparam name="T">the type of property</typeparam>
    /// <param name="current">the current value</param>
    /// <param name="value">the new value</param>
    /// <param name="condition">false causes this method to exit early</param>
    /// <param name="name">the name of the property to set</param>
    public bool SetHardIf<T>(ref T current, T value, bool condition, [CallerMemberName] string name = "")
    {
        if (condition == false) return false;
        current = value;
        FirePropertyChanged(name);
        return true;
    }

    /// <summary>
    /// Subscribes to be notified when the given property changes.  The subscription expires when
    /// the given lifetime manager's lifetime ends.
    /// </summary>
    /// <param name="propertyName">The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be notified of any property change.</param>
    /// <param name="handler">The action to call for notifications</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when the subscription ends</param>
    public void Subscribe(string propertyName, Action handler, ILifetimeManager lifetimeManager) => GetEvent(propertyName).Subscribe(handler, lifetimeManager);
    

    /// <summary>
    ///  Subscribes to be notified once when the given property changes.   
    /// </summary>
    /// <param name="propertyName">The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be notified of any property change.</param>
    /// <param name="handler">The action to call for notifications</param>
    public void SubscribeOnce(string propertyName, Action handler) => GetEvent(propertyName).SubscribeOnce(handler);
    

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

    public void Sync(string propertyName, Action handler, ILifetimeManager lifetimeManager)
    {
        GetEvent(propertyName).Sync(handler, lifetimeManager);
    }

    /// <summary>
    /// Gets a lifetime that represents the value of the given property
    /// </summary>
    /// <param name="propertyName">the property to track</param>
    /// <returns>a lifetime that represents the value of the given property</returns>
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

    /// <summary>
    /// derived types can override
    /// </summary>
    /// <param name="propertyName">the name of the property that was changed</param>
    protected virtual void OnPropertyChanged(string propertyName) { }

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
}
