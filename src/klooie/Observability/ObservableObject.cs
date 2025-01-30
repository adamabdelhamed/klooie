namespace klooie;

/// <summary>
/// interface for an object with observable properties
/// </summary>
public interface IObservableObject
{
    void SubscribeToAnyPropertyChange(object obj, Action<object> handler, ILifetime lifetimeManager);
    void SubscribeOld(string propertyName, Action handler, ILifetime lifetimeManager);
    void SyncOld(string propertyName, Action handler, ILifetime lifetimeManager);
}
