# Observability

Observability allows controls within a klooie application to respond to changes in application state.

## Lifetime

It is very important that observations only take place when they are needed. To facilitate this klooie has a lifetime system.

**Lifetime** is an IDisposable that represents a block of time during your application's lifecycle. ConsoleApp and all ConsoleControl types derive from Lifetime, making it easy to subscribe to observables for the life of a control or the entire app.

And because Lifetime implements IDisposable you can easily have lifetimes that span an async block of code.

```cs

        // SomeCodeThatRunsWhenEnterIsPressed will only be called when the user presses the Enter key AND
        // while the following using block is in scope.
        using (var someLifetime = ConsoleApp.Current.CreateChildLifetime())
        {
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Enter, SomeCodeThatRunsWhenEnterIsPressed, someLifetime);
            await SomeAsyncWork();
            SomeSyncWork();
            await SomeMoreAsyncWork();
        }

```

## Event

You might ask why klooie has its own Event type when C# has events. Well, C# events allow you to subscribe to events without explicitly declaring when you will unsubscribe. This can lead to very strange bugs. Klooie's events require you to declare the subscription lifetime every time you subscribe. 

This is how you define and trigger events.

```cs
    public class SomeCustomControl : CustomControl
    {
        public Event SomethingCoolHappened { get; private init; } = new Event();
        public Event<string> SomethingCoolHappenedWithArgs { get; private init; } = new Event<string>();

        //...

        private void SomethingCool()
        {
            SomethingCoolHappened.Fire();
        }

        private void SomethingCoolWithString()
        {
            SomethingCoolHappenedWithArgs.Fire("Args!");
        }
    }

```

This is how consumers can subscribe to events.

```cs
        var control = new SomeCustomControl();

        // subscribe for just the next firing of this event
        control.SomethingCoolHappened.SubscribeOnce(() => { /* handler here */ });

        // subscribe for the lifetime of the app
        control.SomethingCoolHappened.Subscribe(() => { /* handler here */ }, ConsoleApp.Current);

        // subscribe for the lifetime of the control
        control.SomethingCoolHappened.Subscribe(() => { /* handler here */ }, control);

        // subscribe forever (should rarely be used)
        control.SomethingCoolHappened.Subscribe(() => { /* handler here */}, Lifetime.Forever);

        // subscribe with args
        control.SomethingCoolHappenedWithArgs.SubscribeOnce((stringArgs) => { /* handler here that gets access to the args */ });


```

## ObservableObject

All ConsoleControls derive from ObservableObject. Types that derive from ObservableObject can expose changes to their property values similar to events. Consumers can subscribe to the changes for a given lifetime.

Here is how you define an observable object.

```cs
public class SomeObservableObject : ObservableObject
{
    // This is the easiest way to make a property observable. Just use the base
    // class's Get and Set methods. 
    public string ObservableString { get => Get<string>(); set => Set(value); }
}

```

Here is how you consume an observable object

```cs
        var observable = new SomeObservableObject();

        //Subscribes to changes in ObservableString for the duration of the app
        observable.Subscribe(nameof(observable.ObservableString), () => { }, ConsoleApp.Current);
        
        // same as the above subscription except it also calls the notification callback one time when the subscription is registered
        observable.Sync(nameof(observable.ObservableString), () => { }, ConsoleApp.Current);

```
