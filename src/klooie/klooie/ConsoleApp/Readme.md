The types in this folder represent the main components of a klooie application.

**ConsoleApp** is the main construct that represents an app. It derives from EventLoop. It defines the control tree, exposes focus, handles window resizing, and provides some additional helpers.

**ISoundProvider** is an interface that defines Sound APIs. Unfortunately there is no cross platform sound in .NET. See **klooie.Windows** for an implementation of ISoundProvider that is compatible with klooie on Windows.

**EventLoop** is a loop that has a custom SynchronizationContext which ensures that async calls are processed correctly on the UI thread.
