using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public sealed class Subscription : Recyclable
{
#if DEBUG
    public string? DebugCreationStack;
#endif

    internal Action? Callback;
    internal object? Scope;
    internal object? TScope;
    internal Action<object>? ScopedCallback;
    internal Action<object, object>? TScopedCallback;
    internal IEventT? eventT;

    internal ILifetime? Lifetime { get; private set; }
    internal Recyclable? ToAlsoDispose;
    internal List<Subscription>? Subscribers;

    protected override void OnInit()
    {
#if DEBUG
        //DebugCreationStack = Environment.StackTrace;
#endif
        base.OnInit();
        Reset();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Reset();
    }

    internal void Bind(ILifetime lt)
    {
        Lifetime = lt;
        lt.OnDisposed(this, DisposeMe);
    }

    private static void DisposeMe(object me) => ((Subscription)me)?.TryDispose();

    private void Reset()
    {
        Callback = null;
        ScopedCallback = null;
        TScopedCallback = null;
        Scope = null;
        Lifetime = null;
        Subscribers?.Remove(this);
        Subscribers = null;
        eventT = null;
        TScope = null;
        ToAlsoDispose = null;
    }
}
