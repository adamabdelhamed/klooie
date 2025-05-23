namespace klooie;
public sealed class RecyclableStruct<T> : Recyclable where T : struct
{
    public T Value { get; set; }
    private RecyclableStruct() { }
    public static RecyclableStruct<T> Create() => RecyclableStructPool.Instance.Rent();
    protected override void OnReturn() => Value = default;

    private sealed class RecyclableStructPool : RecycleablePool<RecyclableStruct<T>>
    {
        private static readonly RecyclableStructPool _instance = new RecyclableStructPool();
        public static RecyclableStructPool Instance => _instance;
        private RecyclableStructPool() { }
        public override RecyclableStruct<T> Factory() => new RecyclableStruct<T>();
    }
}
