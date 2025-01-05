namespace klooie;

/// <summary>
/// interface for an object with observable properties
/// </summary>
public interface IObservableObject
{
    void SubscribeToAnyPropertyChange(object obj, Action<object> handler, ILifetimeManager lifetimeManager);
    void SubscribeOld(string propertyName, Action handler, ILifetimeManager lifetimeManager);
    void SyncOld(string propertyName, Action handler, ILifetimeManager lifetimeManager);
}
