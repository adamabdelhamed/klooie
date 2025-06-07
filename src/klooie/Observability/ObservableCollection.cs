using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace klooie;

internal interface IObservableCollection : IEnumerable
{
    int LastModifiedIndex { get; }
    Event<object> Added { get; }
    Event<object> Removed { get; }
    Event Changed { get; }

    void RemoveAt(int index);
    void Insert(int index, object item);
    object this[int index] { get;set; }
}

 
 

/// <summary>
/// An observable list implementation
/// </summary>
/// <typeparam name="T">the type of elements this collection will contain</typeparam>
public sealed class ObservableCollection<T> : Recyclable, IList<T>, IObservableCollection, IReadOnlyList<T>
{
    private List<T> wrapped;
    private Dictionary<T, Recyclable> membershipLifetimes;
    private Event<object> _untypedAdded = Event<object>.Create();
    Event<Object> IObservableCollection.Added => _untypedAdded;
    private Event<object> _untypedRemove = Event<object>.Create();
    Event<Object> IObservableCollection.Removed => _untypedRemove;

    private Event<T> _beforeAdded, added, beforeRemoved, removed;
    private Event changed;
    public int LastModifiedIndex { get; private set; }
    /// <summary>
    /// Called before an item is added to the list
    /// </summary>
    public Event<T> BeforeAdded => _beforeAdded ?? (_beforeAdded = Event<T>.Create());

    /// <summary>
    /// Called after an item is removed from the list
    /// </summary>
    public Event<T> BeforeRemoved => beforeRemoved ?? (beforeRemoved = Event<T>.Create());

    /// <summary>
    /// Called when an element is added to this list
    /// </summary>
    public Event<T> Added => added ?? (added = Event<T>.Create());

    /// <summary>
    /// Called when an element is removed from this list
    /// </summary>
    public Event<T> Removed  => removed ?? (removed = Event<T>.Create());

    /// <summary>
    /// Called whenever this list changes.  You may receive one event for multiple changes
    /// if the changes were atomic (e.g. after calling Clear()).
    /// </summary>
    public Event Changed => changed ?? (changed = Event.Create());


    /// <summary>
    /// Initialized the collection
    /// </summary>
    public ObservableCollection()
    {

    }

    protected override void OnInit()
    {
        base.OnInit();
        wrapped = new List<T>();
        membershipLifetimes = new Dictionary<T, Recyclable>();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _untypedAdded?.Dispose();
        _untypedAdded = null;
        _untypedRemove?.Dispose();
        _untypedRemove = null;
        _beforeAdded?.Dispose();
        _beforeAdded = null;
        added?.Dispose();
        added = null;
        beforeRemoved?.Dispose();
        beforeRemoved = null;
        removed?.Dispose();
        removed = null;
        changed?.Dispose();
        changed = null;
        Clear();
    }

    /// <summary>
    /// Calls the change handler for each existing item, call the change handler once,
    /// and subscribes to future changes
    /// </summary>
    /// <param name="addAction">the add handler</param>
    /// <param name="removeAction">the remove handler</param>
    /// <param name="changedAction">the changed handler</param>
    /// <param name="manager">the lifetime of the subscriptions</param>
    public void Sync(Action<T> addAction, Action<T> removeAction, Action changedAction, ILifetime manager)
    {
        Added.Subscribe(addAction, manager);
        Removed.Subscribe(removeAction, manager);
        if (changedAction != null)
        {
            Changed.Subscribe(changedAction, manager);
        }

        foreach (var obj in this.ToArray())
        {
            addAction(obj);
        }

        changedAction?.Invoke();
    }
 

    /// <summary>
    /// Gets a lifetime that expires when the given item is removed from the collection
    /// </summary>
    /// <param name="item">the item to track</param>
    /// <returns>a lifetime that expires when the given item is removed from the collection</returns>
    public ILifetime GetMembershipLifetime(T item) => membershipLifetimes[item];

    /// <summary>
    /// Fires the Added event for the given item
    /// </summary>
    /// <param name="item">The item that was added</param>
    internal void FireAdded(T item)
    {
        membershipLifetimes.Add(item, DefaultRecyclablePool.Instance.Rent());
        added?.Fire(item);
        _untypedAdded?.Fire(item);
        changed?.Fire();
    }

    /// <summary>
    /// Fired the Removed event for the given item
    /// </summary>
    /// <param name="item">The item that was removed</param>
    internal void FireRemoved(T item)
    {
        removed?.Fire(item);
        _untypedRemove?.Fire(item);
        changed?.Fire();
        var itemLifetime = membershipLifetimes[item];
        membershipLifetimes.Remove(item);
        itemLifetime.TryDispose();
    }

    

    internal void FireBeforeAdded(T item) => _beforeAdded?.Fire(item);
    internal void FireBeforeRemoved(T item) => _beforeAdded?.Fire(item);
    

    /// <summary>
    /// Returns the index of the given item in the list
    /// </summary>
    /// <param name="item">the item to look for</param>
    /// <returns>the index or a negative number if the element is not in the list</returns>
    public int IndexOf(T item) => wrapped.IndexOf(item);

    /// <summary>
    /// Inserts the given item into the list at the specified position
    /// </summary>
    /// <param name="index">the index to insert the item into</param>
    /// <param name="item">the item to insert</param>
    public void Insert(int index, T item)
    {
        LastModifiedIndex = index;
        FireBeforeAdded(item);
        wrapped.Insert(index, item);
        FireAdded(item);
    }

    void IObservableCollection.Insert(int index, object item) => Insert(index, (T)item);

    /// <summary>
    /// Removes the element at the specified index
    /// </summary>
    /// <param name="index">the index of the item to remove</param>
    public void RemoveAt(int index)
    {
        LastModifiedIndex = index;
        var item = wrapped[index];
        FireBeforeRemoved(item);
        wrapped.RemoveAt(index);
        FireRemoved(item);
    }

    /// <summary>
    /// Gets or sets the value at a particular index
    /// </summary>
    /// <param name="index">the index of the item to get or set</param>
    /// <returns>the value at a particular index</returns>
    public T this[int index]
    {
        get
        {
            return wrapped[index];
        }
        set
        {
            var oldItem = wrapped[index];
            if (EqualsSafe(oldItem, value))
            {
                return;
            }

            LastModifiedIndex = index;

            FireBeforeRemoved(oldItem);
            FireBeforeAdded(value);

            wrapped[index] = value;

            FireRemoved(oldItem);
            FireAdded(value);
        }
    }

    public static bool EqualsSafe(object a, object b)
    {
        if (a == null && b == null) return true;
        if (a == null ^ b == null) return false;
        if (object.ReferenceEquals(a, b)) return true;

        return a.Equals(b);
    }

    object IObservableCollection.this[int index]
    {
        get => this[index];
        set => this[index] = (T)value;
    }

    /// <summary>
    /// Adds the given item to the list
    /// </summary>
    /// <param name="item">the item to add</param>
    public void Add(T item)
    {
        LastModifiedIndex = Count;
        FireBeforeAdded(item);
        wrapped.Add(item);
        FireAdded(item);
    }

    /// <summary>
    /// Removes all items from the collection
    /// </summary>
    public void Clear()
    {
        var items = wrapped.ToArray();
        wrapped.Clear();
        LastModifiedIndex = 0;
        for (var i = 0; i < items.Length; i++)
        {
            FireRemoved(items[i]);
        }
    }

    /// <summary>
    /// Tests to see if the list contains the given item
    /// </summary>
    /// <param name="item">the item to look for</param>
    /// <returns>true if the list contains the given item, false otherwise</returns>
    public bool Contains(T item) => wrapped.Contains(item);

    /// <summary>
    /// Copies values from this list into the given array starting at the given index in the destination
    /// </summary>
    /// <param name="array">the destination array</param>
    /// <param name="arrayIndex">the index in the destination array to start the copy</param>
    public void CopyTo(T[] array, int arrayIndex) =>  wrapped.CopyTo(array, arrayIndex);

    /// <summary>
    /// Gets the number of items in the list
    /// </summary>
    public int Count => wrapped.Count;
    /// <summary>
    /// Always returns false
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Removes the given item from the list
    /// </summary>
    /// <param name="item">the item to remove</param>
    /// <returns>true if an item was removed, false if the item was not found in the list</returns>
    public bool Remove(T item)
    {
        if (wrapped.Contains(item))
        {
            FireBeforeRemoved(item);

            for (var i = 0; i < wrapped.Count; i++)
            {
                if (EqualsSafe(wrapped[i], item))
                {
                    LastModifiedIndex = i;
                    wrapped.RemoveAt(i);
                    break;
                }
            }
            FireRemoved(item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets an enumerator for this list
    /// </summary>
    /// <returns>an enumerator for this list</returns>
    public IEnumerator<T> GetEnumerator() => wrapped.GetEnumerator();

    /// <summary>
    /// Gets an enumerator for this list
    /// </summary>
    /// <returns>an enumerator for this list</returns>
    IEnumerator IEnumerable.GetEnumerator() => wrapped.GetEnumerator();
}

public class ObservableCollectionPool<T> : RecycleablePool<ObservableCollection<T>>
{

    private static ObservableCollectionPool<T> instance;
    public static ObservableCollectionPool<T> Instance => instance ?? (instance = new ObservableCollectionPool<T>());

    public override ObservableCollection<T> Factory()
    {
        return new ObservableCollection<T>();
    }
}
