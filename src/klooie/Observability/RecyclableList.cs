namespace klooie;

public class RecyclableList<T> : Recyclable, System.Collections.IEnumerable
{
    private List<T> _items;
    public List<T> Items => _items;

    public RecyclableList()
    {
        _items =  new List<T>();
    }

    /// <summary>
    /// Ensures the capacity of the internal list is at least the specified value.
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        if (_items.Capacity < capacity)
        {
            _items.Capacity = capacity;
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _items.Clear();
    }

    public T this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public int Count => _items.Count;
    public System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
}

public class RecyclableListPool<T> : RecycleablePool<RecyclableList<T>>
{
    private static readonly RecyclableListPool<T> _instance = new RecyclableListPool<T>();
    public static RecyclableListPool<T> Instance => _instance;
    private RecyclableListPool() { }

    public override RecyclableList<T> Factory() => new RecyclableList<T>();

    // New method to rent with capacity
    public RecyclableList<T> Rent(int requiredCapacity)
    {
        var list = Rent();
        list.EnsureCapacity(requiredCapacity);
        return list;
    }
}
