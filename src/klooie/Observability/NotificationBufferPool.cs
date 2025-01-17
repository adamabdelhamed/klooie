namespace klooie;
internal sealed class NotificationBufferPool
{
#if DEBUG
    internal static int HitCount = 0;
    internal static int MissCount = 0;
    internal static int PartialHitCount = 0;
#endif
    private List<Subscription[]> pool = new List<Subscription[]>();
    public static readonly NotificationBufferPool Instance = new NotificationBufferPool();
    private readonly System.Threading.Lock _lock = new System.Threading.Lock();

    private NotificationBufferPool() { }

    public Subscription[] Get(int mainSize)
    {
        lock (_lock)
        {
            mainSize = mainSize < 10 ? 10 : mainSize;
            // if no buffers, create one
            if (pool.Count == 0)
            {
#if DEBUG
                MissCount++;
#endif
                return new Subscription[mainSize * 2];
            }

            // try to find an existing buffer that is big enough
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i].Length >= mainSize)
                {
                    var toRent = pool[i];
                    pool.RemoveAt(i);
#if DEBUG
                    HitCount++;
#endif
                    return toRent;
                }
            }

#if DEBUG
            PartialHitCount++;
#endif
            var resized = new Subscription[mainSize * 2];
            // resize existing buffer to be big enough and use it
            pool[0] = resized;
            pool.RemoveAt(0);
            return resized;
        }
    }

    public void Return(Subscription[] buffer)
    {
        lock (_lock)
        {
            Array.Clear(buffer, 0, buffer.Length);
            pool.Add(buffer);
        }
    }

    public static void Notify(List<Subscription>? subscribers)
    {
        if (subscribers == null || subscribers.Count == 0) return;
        var subscriberCount = subscribers.Count;

        // Perf optimization for the very common 1 subscriber case since there's no need to copy the list,
        // rent a buffer, or bother with a loop and lifetime checks.
        if (subscriberCount == 1)
        {
            Notify(subscribers[0]);
            return;
        }

        // buffer used to allow concurrent modification to subscribers during notification
        // buffer also reduces memory allocations insead of doing a ToArray() on the list
        var buffer = Instance.Get(subscriberCount);

        try
        {
            // copy the current subscribers into the buffer
            for (var i = 0; i < subscriberCount; i++)
            {
                buffer[i] = subscribers[i];
            }

            // now notify
            for (var i = 0; i < subscriberCount; i++)
            {
                var sub = buffer[i];
                if (sub.Lifetime?.IsExpired == false)
                {
                    Notify(sub);
                }
            }
        }
        finally
        {
            Instance.Return(buffer);
        }
    }

    private static void Notify(Subscription sub)
    {
        // NEW OR MODIFIED CODE: if we have a ScopedCallback, call it; otherwise use Callback
        if (sub.ScopedCallback != null)
        {
            sub.ScopedCallback(sub.Scope);
        }
        else
        {
            sub.Callback?.Invoke();
        }
    }
}

public class Subscription
{
    // Standard callback (no scope)
    public Action? Callback { get; set; }

    // Scoped callback with a strongly-typed scope
    internal object? Scope;
    internal Action<object>? ScopedCallback;

    public ILifetimeManager? Lifetime;
    public List<Subscription>? Subscribers;

    internal void Reset()
    {
        Callback = null;
        ScopedCallback = null;
        Scope = null;
        Lifetime = null;
        Subscribers?.Remove(this);
        Subscribers = null;
    }

    internal Subscription() { }
}
