namespace klooie;

 

/// <summary>
/// Extension methods for lifetime
/// </summary>
public static class ILifetimeEx
{

}
 
public interface ILifetime
{
    public bool IsExpired { get; }
    public bool IsExpiring { get; }
    public bool ShouldContinue { get; }
    public bool ShouldStop { get; }
    public void OnDisposed(Action cleanupCode);
    public void OnDisposed(object scope, Action<object> cleanupCode);
    public void OnDisposed(Recyclable obj);
}