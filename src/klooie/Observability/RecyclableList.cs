namespace klooie;

public class RecyclableList<T> : Recyclable, System.Collections.IEnumerable
{
    private List<T> _items = new List<T>();
    public List<T> Items => _items;
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
    public System.Collections.IEnumerator GetEnumerator() =>  _items.GetEnumerator();
}

public class RecyclableListPool<T> : RecycleablePool<RecyclableList<T>>
{
    private static readonly RecyclableListPool<T> _instance = new RecyclableListPool<T>();
    public static RecyclableListPool<T> Instance => _instance;
    private RecyclableListPool() { }
    public override RecyclableList<T> Factory() => new RecyclableList<T>();
}