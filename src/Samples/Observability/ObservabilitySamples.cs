namespace klooie.Samples;

internal partial class ObservabilitySamples
{
    public static async Task Foo()
    {
        //#Sample -id LifetimeDisposable

        // SomeCodeThatRunsWhenEnterIsPressed will only be called when the user presses the Enter key AND
        // while the following using block is in scope.
        var someLifetime = ConsoleApp.Current.CreateChildRecyclable();
        
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Enter, SomeCodeThatRunsWhenEnterIsPressed, someLifetime);
        await SomeAsyncWork();
        SomeSyncWork();
        await SomeMoreAsyncWork();
        someLifetime.Dispose();
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


        // subscribe with args
        control.SomethingCoolHappenedWithArgs.SubscribeOnce((stringArgs) => { /* handler here that gets access to the args */ });

//#EndSample
    }

    //#Sample -id ObservableObjectSampleDefined
    public partial class SomeObservableObject : IObservableObject
    {
        // This is the easiest way to make a property observable. Just use the base
        // class's Get and Set methods. 
        public partial string ObservableString { get; set; }
    }
    //#EndSample

    private static void Observer()
    {
//#Sample -id ObservableObjectSampleConsumed
        var observable = new SomeObservableObject();
        observable.ObservableStringChanged.Subscribe(() => { /* handler here */ }, ConsoleApp.Current);
        //Subscribes to changes in ObservableString for the duration of the app


        // same as the above subscription except it also calls the notification callback one time when the subscription is registered
        observable.ObservableStringChanged.Sync(() => { /* handler here */ }, ConsoleApp.Current);
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
