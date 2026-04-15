using System.Runtime.CompilerServices;

namespace klooie;

/// <summary>
/// Extension methods for lifetime
/// </summary>
public static class Lifetime
{
    public static readonly ILifetime Completed = CreateCompleted();

    public static readonly ILifetime Forever = new Recyclable(); // Intentionally not from the pool
    private static ILifetime CreateCompleted()
    {
        var lt = new Recyclable(); // Intentionally not from the pool
        lt.Dispose("external/klooie/src/klooie/Observability/Lifetime.cs:1");
        return lt;
    }

    public static void OnDisposedOrNow(this ILifetime lifetime, Action cleanupCode)
    {
        if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
        if (cleanupCode == null) throw new ArgumentNullException(nameof(cleanupCode));

        if (lifetime.IsStillValid(lifetime.Lease))
        {
            lifetime.OnDisposed(cleanupCode);
        }
        else
        {
            cleanupCode();
        }
    }

    public static void OnDisposedOrNow<T>(this ILifetime lifetime, T scope, Action<T> cleanupCode)
    {
        if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (cleanupCode == null) throw new ArgumentNullException(nameof(cleanupCode));

        if (lifetime.IsStillValid(lifetime.Lease))
        {
            lifetime.OnDisposed(scope, cleanupCode);
        }
        else
        {
            cleanupCode(scope);
        }
    }
}
 
public interface ILifetime
{
    int Lease { get; }
    bool IsStillValid(int lease);
    public void OnDisposed(Action cleanupCode);
    public void OnDisposed<T>(T scope, Action<T> cleanupCode);
}

public static class LifetimeAwaitable
{
    public static LifetimeAwaiter GetAwaiter(this ILifetime lifetime) => new(lifetime);

    public readonly struct LifetimeAwaiter : ICriticalNotifyCompletion
    {
        private readonly ILifetime lifetime;
        private readonly int lease;
        public LifetimeAwaiter(ILifetime lifetime)
        {
            this.lifetime = lifetime;
            lease = lifetime.Lease;
        }

        public bool IsCompleted => !lifetime.IsStillValid(lease);        
        public void OnCompleted(Action continuation) => lifetime.OnDisposed(continuation);
        public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
        public void GetResult() { }
    }
}
