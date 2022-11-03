namespace klooie.Samples;

internal class ObservabilitySamples
{
    public static async Task Foo()
    {
//#Sample -id LifetimeDisposable

        // SomeCodeThatRunsWhenEnterIsPressed will only be called when the user presses the Enter key AND
        // while the following using block is in scope.
        using (var someLifetime = ConsoleApp.Current.CreateChildLifetime())
        {
            ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Enter, SomeCodeThatRunsWhenEnterIsPressed, someLifetime);
            await SomeAsyncWork();
            SomeSyncWork();
            await SomeMoreAsyncWork();
        }
//#EndSample

        


    }

//#Sample -id EventBasicUsageDeclarer
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
//#EndSample

    private static void Bar()
    {
//#Sample -id EventBasicUsageConsumer
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

//#EndSample
    }

//#Sample -id ObservableObjectSampleDefined
    public class SomeObservableObject : ObservableObject
    {
        // This is the easiest way to make a property observable. Just use the base
        // class's Get and Set methods. 
        public string ObservableString { get => Get<string>(); set => Set(value); }
    }
//#EndSample

    private static void Observer()
    {
//#Sample -id ObservableObjectSampleConsumed
        var observable = new SomeObservableObject();

        //Subscribes to changes in ObservableString for the duration of the app
        observable.Subscribe(nameof(observable.ObservableString), () => { }, ConsoleApp.Current);
        
        // same as the above subscription except it also calls the notification callback one time when the subscription is registered
        observable.Sync(nameof(observable.ObservableString), () => { }, ConsoleApp.Current);
//#EndSample
    }

    private static void SomeSyncWork()
    {
        throw new NotImplementedException();
    }

    private static Task SomeMoreAsyncWork()
    {
        throw new NotImplementedException();
    }

    private static Task SomeAsyncWork()
    {
        throw new NotImplementedException();
    }

    private static void SomeCodeThatRunsWhenEnterIsPressed()
    {
        throw new NotImplementedException();
    }
}
