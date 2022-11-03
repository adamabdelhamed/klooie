namespace klooie;
internal sealed class NotificationBufferPool
{
#if DEBUG
    internal static int HitCount = 0;
    internal static int MissCount = 0;
    internal static int PartialHitCount = 0;
#endif
    private List<NotificationBuffer> pool = new List<NotificationBuffer>();
    public static readonly NotificationBufferPool Instance = new NotificationBufferPool();

    public NotificationBuffer Get(int mainSize, int paramSize)
    {
        lock (pool)
        {
            mainSize = mainSize < 100 ? 100 : mainSize;
            paramSize = paramSize < 100 ? 100 : paramSize;
            // if no buffers, create one
            if (pool.Count == 0)
            {
#if DEBUG
                MissCount++;
#endif
                return new NotificationBuffer()
                {
                    MainBuffer = new Subscription[mainSize * 2],
                    ParamBuffer = new SubscriptionWithParam[paramSize * 2]
                };
            }

            // try to find an existing buffer that is big enough
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i].MainBuffer.Length >= mainSize && pool[i].ParamBuffer.Length >= paramSize)
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

            // resize existing buffer to be big enough and use it
            var firstBuffer = pool.First();
            firstBuffer.MainBuffer = new Subscription[mainSize * 2];
            firstBuffer.ParamBuffer = new SubscriptionWithParam[paramSize * 2];
            pool.RemoveAt(0);
            return firstBuffer;
        }
    }

    public void Return(NotificationBuffer buffer)
    {
        lock (pool)
        {
            pool.Add(buffer);
        }
    }

    public static void Notify(List<Subscription> subscribers, List<SubscriptionWithParam> subscribersWithParams)
    {
        var subscriberCount = subscribers.Count;
        var subscriberCountWithParams = subscribersWithParams.Count;
        var buffer = Instance.Get(subscriberCount, subscriberCountWithParams);
        try
        {
            var subSpan = buffer.MainBuffer.AsSpan();
            var paramSpan = buffer.ParamBuffer.AsSpan();

            for (var i = 0; i < subscriberCount; i++)
            {
                subSpan[i] = subscribers[i];
            }

            for (var i = 0; i < subscriberCountWithParams; i++)
            {
                paramSpan[i] = subscribersWithParams[i];
            }

            for (var i = 0; i < subscriberCount; i++)
            {
                if (subSpan[i].Lifetime.IsExpired == false)
                {
                    subSpan[i].Callback();
                }
            }

            for (var i = 0; i < subscriberCountWithParams; i++)
            {
                if (paramSpan[i].Lifetime.IsExpired == false)
                {
                    paramSpan[i].Callback(paramSpan[i].Param);
                }
            }
        }
        finally
        {
            Instance.Return(buffer);
        }
    }
}

internal class Subscription
{
    public Action Callback { get; set; }
    public ILifetimeManager Lifetime { get; set; }
}

internal class SubscriptionWithParam
{
    public Action<object> Callback { get; set; }
    public ILifetimeManager Lifetime { get; set; }
    public object Param { get; set; }
}

internal class NotificationBuffer
{
    public Subscription[] MainBuffer;
    public SubscriptionWithParam[] ParamBuffer;
}