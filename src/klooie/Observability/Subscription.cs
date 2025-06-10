using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
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
    public Action Callback { get; set; }

    public override void Notify()
    {
        Callback.Invoke();
    }
}

internal sealed class ScopedSubscription<T> : Subscription
{
    public T Scope { get; set; }
    public Action<T> ScopedCallback { get; set; }

    public override void Notify()
    {
        ScopedCallback.Invoke(Scope);
    }
}

internal sealed class ScopedArgsSubscription<TScope, TArgs> : Subscription
{
    public TArgs Args { get; set; }
    public TScope Scope { get; set; }
    public Action<TScope, TArgs> ScopedCallback { get; set; }

    public override void Notify()
    {
        ScopedCallback.Invoke(Scope, Args);
    }
}
