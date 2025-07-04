﻿## Advanced Looping

The EventLoop class uses the InnerLoopAPIs to provide advanced delay semantics. They don't rely on Task or await/async since those allocate.

- [ ] InnerLoopAPIs should be used for high-frequency events

When asked to refactor an async away to a zero allocation delay, use this example as a reference.

## Before Refactoring

```csharp
public class SomeClass
{
	void SomeSchedulerMethod()
	{
	    ConsoleApp.Current.Invoke(()=> SomeAsyncMethod());
	}

	async Task SomeAsyncMethod()
	{
		// This method is using async/await and may allocate
		await Task.Delay(1000);
		Foo();
	}

	void Foo()
	{
		// Do something
	}
}
```

## After Refactoring

```csharp
public class SomeClass
{
	void SomeSchedulerMethod() => SomeInnerLoopMethod();
	
	// Calls Foo after one second as long as the app is still running
	void SomeInnerLoopMethod() => ConsoleApp.Current.EventLoop.Delay(this, 1000, Foo);
	
	static void Foo(object me) // static so that it doesn't allocate, requires casting to marshall the instance
	{
	    var _this = (SomeClass)me;
		// Do something with _this_
	}
}
```

## After Refactoring (Recycling Example)

Sometimes the code scheduling the work depends on pooled objects that have lifetimes. The pattern below shows how we should be managing this.

```csharp
public class SomeClass
{
	private Recyclable recyclableIDependOn;

	void SomeSchedulerMethod() => SomeInnerLoopMethod();
	
	void SomeInnerLoopMethod() 
	{
		// Foo will only be called if all dependencies are still valid. In this case, SomeClass is not itself Recyclable, but if it were
		// we could capture a local reference to the state before passing it to DelayIfValid and then call **AddDependency()** on it so that
		// both the SomeClass instance and the recyclableIDependOn are considered required for Foo to be called after 1 second.
		//	- The state object itself will be recycled by the EventLoop in the case that Foo is not called.
		//	- If Foo is called then this class must manage the recycling of the state object since it may want to loop via another delay.
		ConsoleApp.Current.EventLoop.DelayIfValid(SomeClassState.Create(recyclableIDependOn), 1000, Foo);
	}
	
	// static so that it doesn't allocate, requires casting to marshall the instance
	// Rule: Avoid closures/lambda captures in scheduling callbacks; always use static methods and pass all required state as an argument
	// This pattern supports a game engine that desired zero GC during gameplay.
	static void Foo(object stateObj) 
	{
	    var state = (SomeClassState)stateObj;
		var instance = state.Instance;
		state.TryDispose(); // Because we're done with the delays. If we were implementing a loop we would reuse this state object until the loop is done.
		// Do something with _this.Instance
	}
}

// Proper way to create a custom delay state. If you don't need access to additional state in your callback then just call DelayState.Create to manage your depenencies.
public class SomeClassState : DelayState
{
	public SomeClass Instance { get; private set; }

	private SomeClassState(){}

	public static SomeClassState Create(Recyclable recyclable)
	{
		var state = Pool.Instance.Rent();
		state.AddDependency(recyclable);
		return state;
	}

	protected override void OnReturn()
	{
		base.OnReturn();
		Instance = null; 
	}

	// Note that there are some Recyclable classes that have public constructors. That is an old pattern.
	// In those cases the pool is generated by a source generator. I'll eventually update the generator to
	// generate this pool, but it will require the class to be partial.
	private class Pool : RecycleablePool<SomeClassState>
	{
		private Pool? instance;
		public static Pool Instance => instance ??= new Pool();
		protected override Factory() => new SomeClassState();
	}
}

```