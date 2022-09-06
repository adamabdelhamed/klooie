namespace klooie.Gaming.Code;
public class Heap : ObservableObject
{
    public static Heap Current { get; private set; }
    public Heap(ILifetimeManager lt)
    {
        if (Current != null) throw new NotSupportedException("There can only be one heap at a time");
        Current = this;
        lt.OnDisposed(() => Current = null);
    }
}

