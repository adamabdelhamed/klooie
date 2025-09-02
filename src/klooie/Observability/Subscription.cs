using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

internal interface ISubscription
{
    int ThreadId { get; }
    int Lease { get; }
    bool IsStillValid(int lease);
    void Notify();
    void Dispose(string reason = null);
    bool TryDispose(int lease, string reason = null);
    bool TryDispose(string reason = null);  
}

internal abstract class Subscription : Recyclable, ISubscription
{
    internal ILifetime? Lifetime { get; private set; }

    protected override void OnReturn()
    {
        base.OnReturn();
        Lifetime = null;
    }

    public abstract void Notify();
}

internal sealed class ActionSubscription : Subscription
{
    private ActionSubscription() { }
    private static LazyPool<ActionSubscription> pool = new LazyPool<ActionSubscription>(() => new ActionSubscription());
    public static ActionSubscription Create(Action a)
    {
        var sub = pool.Value.Rent();
        sub.Callback = a;
        return sub;
    }

    public Action Callback { get; private set; }

    public override void Notify()
    {
        Callback?.Invoke();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Callback = null;
    }
}

internal sealed class ScopedSubscription<T> : Subscription
{
    public T Scope { get; private set; }
    public Action<T> ScopedCallback { get; private set; }

    private ScopedSubscription() { }
    private static LazyPool<ScopedSubscription<T>> pool = new LazyPool<ScopedSubscription<T>>(() => new ScopedSubscription<T>());
    public static ScopedSubscription<T> Create(T scope, Action<T> callback)
    {
        var sub = pool.Value.Rent();
        sub.ScopedCallback = callback;
        sub.Scope = scope;
        return sub;
    }

    public override void Notify()
    {
        ScopedCallback?.Invoke(Scope);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        ScopedCallback = null;
        Scope = default(T);
    }
}

internal class ArgsSubscription<TArgs> : Subscription
{
    public TArgs Args { get; protected set; }
    public void SetArgs(TArgs args) => Args = args;
    public Action<TArgs> ArgsCallback { get; private set; }

    protected ArgsSubscription() { }
    private static LazyPool<ArgsSubscription< TArgs>> pool = new LazyPool<ArgsSubscription< TArgs>>(static () => new ArgsSubscription< TArgs>());
    public static ArgsSubscription<TArgs> Create(Action<TArgs> callback)
    {
        var sub = pool.Value.Rent();
        sub.ArgsCallback = callback;
        return sub;
    }

    public override void Notify()
    {
        ArgsCallback?.Invoke(Args);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        ArgsCallback = null;
        Args = default(TArgs);
    }
}

internal sealed class ScopedArgsSubscription<TScope, TArgs> : ArgsSubscription<TArgs>
{
    public TScope Scope { get; private set; }
    public Action<TScope, TArgs> ScopedCallback { get; private set; }

    private static LazyPool<ScopedArgsSubscription<TScope, TArgs>> pool = new LazyPool<ScopedArgsSubscription<TScope, TArgs>>(static () => new ScopedArgsSubscription<TScope, TArgs>());
    public static ScopedArgsSubscription<TScope,TArgs> Create(TScope scope, Action<TScope,TArgs> callback)
    {
        var sub = pool.Value.Rent();
        sub.ScopedCallback = callback;
        sub.Scope = scope;
        return sub;
    }

    public override void Notify()
    {
        ScopedCallback?.Invoke(Scope, Args);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        ScopedCallback = null;
        Scope = default(TScope);
    }
}

internal sealed class OnceActionSubscription : Subscription
{
    private static LazyPool<OnceActionSubscription> pool = new LazyPool<OnceActionSubscription>(() => new OnceActionSubscription());

    private Action? callback;

    private OnceActionSubscription() { }

    public static OnceActionSubscription Create(Action callback)
    {
        var sub = pool.Value.Rent();
        sub.callback = callback;
        return sub;
    }

    public override void Notify()
    {
        try { callback?.Invoke(); }
        finally
        {
            Dispose();
        }
    }

    protected override void OnReturn()
    { 
        callback = null;
    }
}

internal sealed class OnceScopedSubscription<T> : Subscription
{
    private static LazyPool<OnceScopedSubscription<T>> pool = new LazyPool<OnceScopedSubscription<T>>(() => new OnceScopedSubscription<T>());

    private Action<T>? callback;
    private T scope;

    private OnceScopedSubscription() { }

    public static OnceScopedSubscription<T> Create(T scope, Action<T> callback)
    {
        var sub = pool.Value.Rent();
        sub.scope = scope;
        sub.callback = callback;
        return sub;
    }

    public override void Notify()
    {
        try { callback?.Invoke(scope); }
        finally { Dispose(); }
    }

    protected override void OnReturn()
    {
        callback = null;
        scope = default!;
    }
}

internal sealed class OnceArgsSubscription<TArgs> : ArgsSubscription<TArgs>
{
    private static LazyPool<OnceArgsSubscription<TArgs>> pool = new LazyPool<OnceArgsSubscription<TArgs>>(() => new OnceArgsSubscription<TArgs>());

    private Action<TArgs>? callback;

    private OnceArgsSubscription() { }

    public static OnceArgsSubscription<TArgs> Create(Action<TArgs> callback)
    {
        var sub = pool.Value.Rent();
        sub.callback = callback;
        return sub;
    }

    public override void Notify()
    {
        try { callback?.Invoke(Args); }
        finally { Dispose(); }
    }

    protected override void OnReturn()
    {
        callback = null;
        Args = default!;
    }
}


internal sealed class OnceScopedArgsSubscription<TScope, TArgs> : ArgsSubscription<TArgs>
{
    private static LazyPool<OnceScopedArgsSubscription<TScope, TArgs>> pool =
        new LazyPool<OnceScopedArgsSubscription<TScope, TArgs>>(() => new OnceScopedArgsSubscription<TScope, TArgs>());

    private Action<TScope, TArgs>? callback;
    private TScope scope;

    private OnceScopedArgsSubscription() { }

    public static OnceScopedArgsSubscription<TScope, TArgs> Create(TScope scope, Action<TScope, TArgs> callback)
    {
        var sub = pool.Value.Rent();
        sub.scope = scope;
        sub.callback = callback;
        return sub;
    }

    public override void Notify()
    {
        try { callback?.Invoke(scope, Args); }
        finally { Dispose(); }
    }

    protected override void OnReturn()
    {
        callback = null;
        scope = default!;
        Args = default!;
    }
}
