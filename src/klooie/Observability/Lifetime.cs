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
        lt.Dispose();
        return lt;
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