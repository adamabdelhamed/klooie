using Newtonsoft.Json;

namespace ScrollSucker;
public abstract class SpawnDirective : IObservableObject
{
    protected ObservableObject observable = new ObservableObject();
    [FormIgnore]
    public float X { get => observable.Get<float>(); set => observable.Set(value); }
    [FormIgnore]
    public bool Top { get => observable.Get<bool>(); set => observable.Set(value); }

    public void Subscribe(string p, Action h, ILifetimeManager l) => observable.Subscribe(p, h, l);
    public void Sync(string p, Action h, ILifetimeManager l) => observable.Sync(p, h, l);
    public object GetPrevious(string p) => observable.GetPrevious<object>(p);
    public T Get<T>(string name) => observable.Get<T>(name);
    public void Set<T>(T value, string name) => observable.Set(value, name);
    public ILifetimeManager GetPropertyValueLifetime(string p) => observable.GetPropertyValueLifetime(p);

    public abstract GameCollider Preview(World w);
    public abstract void Render(World w);
    public abstract void ValidateUserInput();
    public abstract void RemoveFrom(LevelSpec spec);
    public abstract void AddTo(LevelSpec spec);

    public SpawnDirective Clone()
    {
        var settings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
        };

        var json = JsonConvert.SerializeObject(this, settings);
        var ret = JsonConvert.DeserializeObject<SpawnDirective>(json, settings);
        return ret;
    }
}