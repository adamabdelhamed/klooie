# Observability

Observability allows controls within a klooie application to respond to changes in application state.

## Lifetime

It is very important that observations only take place when they are needed. To facilitate this klooie has a lifetime system.

**Lifetime** is an IDisposable that represents a block of time during your application's lifecycle. ConsoleApp and all ConsoleControl types derive from Lifetime, making it easy to subscribe to observables for the life of a control or the entire app.

And because Lifetime implements IDisposable you can easily have lifetimes that span an async block of code.

//#LifetimeDisposable

## Event

You might ask why klooie has its own Event type when C# has events. Well, C# events allow you to subscribe to events without explicitly declaring when you will unsubscribe. This can lead to very strange bugs. Klooie's events require you to declare the subscription lifetime every time you subscribe. 

This is how you define and trigger events.

//#EventBasicUsageDeclarer

This is how consumers can subscribe to events.

//#EventBasicUsageConsumer

## ObservableObject

All ConsoleControls derive from ObservableObject. Types that derive from ObservableObject can expose changes to their property values similar to events. Consumers can subscribe to the changes for a given lifetime.

Here is how you define an observable object.

//#ObservableObjectSampleDefined

Here is how you consume an observable object

//#ObservableObjectSampleConsumed