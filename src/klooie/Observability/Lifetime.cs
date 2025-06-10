namespace klooie;

 

/// <summary>
/// Extension methods for lifetime
/// </summary>
public static class ILifetimeEx
{

}
 
public interface ILifetime
{
    int Lease { get; }
    bool IsStillValid(int lease);
    public void OnDisposed(Action cleanupCode);
    public void OnDisposed(object scope, Action<object> cleanupCode);
}