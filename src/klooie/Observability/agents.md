## Recyclable Pattern

- This repo uses a Recyclable pattern for classes that need to be pooled and reused instead of created and destroyed frequently. This helps reduce memory allocations and improve performance.
- Any code can rent a Recyclable from the Pool using a *Create()* factory method. When that code is done using the Recyclable it must call *Dispose* if it is sure it is alive, or *TryDispose* if it is not sure. 
- If a method accepts a Recyclable as a parameter then it is the caller's responsibility to ensure that the recyclable is alive before passing it.
    - If there is any chance that the recyclable might be disposed outside of the called method then the called method should capture a local reference to the recyclable's Lease property, which is an integer.
        - Before using the recyclable, especially after any sort of async delay, the method should call myRecyclable.IsStillValid(capturedLease).
        - Code should be intentional about who owns the lifetime management. Sometimes the creater owns it, sometimes the caller does, and sometimes the callee does. The Lease property helps manage this ownership.
    - The Lease property allows the code to safely dispose of the Recyclable when it is done, without worrying about it being returned to the pool prematurely.
- The Recyclable will then be returned to the pool and reset to its default state, ready for reuse.
- When other pooled types (Recyclable Class) create child recyclables, like Events, they must lazily initialize them and then call TryDispose() and null them in their own OnReturn() override.
- Events are thread safe, but all klooie applications are managed by a single threaded event loop so most code using them doesn't have to be thread safe.

### Recyclable Pattern Summary / Example
- [ ] Lazy initialize child recyclables
- [ ] Dispose (TryDispose) and null children in OnReturn()
- [ ] Use Construct() instead of parameterized constructors
- [ ] Reset all fields to defaults in OnReturn()
- [ ] Use Pool.Instance.Rent() and ensure Pool.Factory() returns new instance
- [ ] Review and refactor old code using Bind or with missing disposal

Example of a Recyclable class that follows the pattern:
```csharp
public class MyRecyclable : Recyclable
{
    private static readonly Lazy<FuncPool<MyRecyclable>> MyRecyclablePool = new Lazy<FuncPool<MyRecyclable>>(static () => new FuncPool<MyRecyclable>(static () => new MyRecyclable()));

    private Event innerEvent; // Event is itself Recyclable so we want to lazily initialize it and manage its lifecycle.
    public Event InnerEvent => innerEvent ??= Event.Create();

    private int someField;

    private MyRecyclable() { }

    public static MyRecyclable Create(int someFieldToInitialize)
    {
        var ret = MyRecyclablePool.Value.Rent();
        ret.Construct(someFieldToInitialize);
        return ret;
    }

    // Since we can't have a constructor with parameters in a Recyclable,
    // we have a pattern where we have a Construct method that initializes fields.
    // It is protected so that subclasses can define their own Construct methods 
    // with their own parameters if needed. Subclasses that define their own Create
    // methods should call the base Construct method to ensure proper initialization.
    protected void Construct(int someFieldToInitialize)
    {
        someField = someFieldToInitialize;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        innerEvent?.TryDispose();
        innerEvent = null;
        someField = default;
    }
}
```

### What to Refactor
- Classes with `Bind` instead of `Construct` should be updated to use `Construct`.
- All Recyclables must dispose of child recyclables in `OnReturn`.
- Any usage of Recyclable without proper Dispose/TryDispose should be updated.
- Ensure all fields are reset to default values in `OnReturn`.
- 
There are other cases where the code that creates a Recyclable is not itself a Recyclable then it must call Dispose() or TryDispose() when it knows it's done using it.

There are many examples of code in the repo that have not yet been updated to use this pattern perfectly. Some code has a Bind method instead of Construct. Other code Creates Recyclables, but does not dispose of them properly. 

The goal is to refactor these over time. If you are ever asked to do general maintainance on the codebase, please look for these patterns and update them to use the Recyclable pattern described above.

### Golden Examples
- EventThrottle.cs - good example of a Recyclable that uses the pattern correctly and it lives in the same folder as this document.
