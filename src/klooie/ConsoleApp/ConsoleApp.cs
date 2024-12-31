namespace klooie;

/// <summary>
/// A class representing a console application that uses a message pump to synchronize work on a UI thread
/// </summary>
public class ConsoleApp : EventLoop
{
    [ThreadStatic]
    private static ConsoleApp? _current;

    /// <summary>
    /// Gets a reference to the current app running on this thread.  This will only be populated by the thread
    /// that is running the message pump (i.e. it will never be your main thread).
    /// </summary>
    public static ConsoleApp? Current => _current;

    private FocusManager focus;
    private LayoutRootPanel layoutRoot;

    /// <summary>
    /// When key throttling is enabled this lets you set the minimum time that must
    /// elapse before we forward a key press to the app, provided it is the same key
    /// that was most recently pressed.
    /// </summary>
    public TimeSpan MinTimeBetweenKeyPresses { get => focus.MinTimeBetweenKeyPresses; set => focus.MinTimeBetweenKeyPresses = value; } 
    /// <summary>
    /// True by default. When true, discards key presses that come in too fast
    /// likely because the user is holding the key down. You can set the
    /// MinTimeBetweenKeyPresses property to suit your needs.
    /// </summary>
    public bool KeyThrottlingEnabled { get => focus.KeyThrottlingEnabled; set => focus.KeyThrottlingEnabled = value; }

    /// <summary>
    /// An event that fires when key input has been throttled. Only fired
    /// when KeyThrottlingEnabled is true.
    /// </summary>
    public Event OnKeyInputThrottled => focus.OnKeyInputThrottled;

    /// <summary>
    /// An event that fires when the current focus stack depth changes
    /// </summary>
    public Event FocusStackDepthChanged => focus.StackDepthChanged;

    /// <summary>
    /// An event that fires when the focused control changes
    /// </summary>
    public Event FocusChanged => focus.FocusedControlChanged;

    /// <summary>
    /// Gets the current focused control
    /// </summary>
    public ConsoleControl FocusedControl => focus.FocusedControl;

    /// <summary>
    /// Gets the current focus stack depth
    /// </summary>
    public int FocusStackDepth => focus.StackDepth;

    /// <summary>
    /// An event that fires whenever a key is pressed. Other handlers will also run.
    /// </summary>
    public Event<ConsoleKeyInfo> GlobalKeyPressed => focus.GlobalKeyPressed;

    /// <summary>
    /// Gets the width of the app
    /// </summary>
    public int Width => LayoutRoot.Width;
    /// <summary>
    /// Gets the height of the app
    /// </summary>
    public int Height => LayoutRoot.Height;

    /// <summary>
    /// An event that fires when the user resizes the window. The Bounds of
    /// LayoutRoot will be updated before this event fires.
    /// </summary>
    public Event OnWindowResized => layoutRoot.OnWindowResized;

    /// <summary>
    /// If true, paint requests will be honored. Defaults to true.
    /// </summary>
    public bool PaintEnabled {get => layoutRoot.PaintEnabled;set => layoutRoot.PaintEnabled = value; }  

    /// <summary>
    /// If true, clears the console when the app exits. Defaults to true.
    /// </summary>
    public bool ClearOnExit  {get => layoutRoot.ClearOnExit; set => layoutRoot.ClearOnExit = value; } 

    /// <summary>
    /// Gets the total number of event loop cycles that have run
    /// </summary>
    public int TotalCycles => layoutRoot.TotalCycles;

    /// <summary>
    /// Gets the current frame rate for the app
    /// </summary>
    public int CyclesPerSecond => layoutRoot.CyclesPerSecond;

    /// <summary>
    /// Gets the current paint rate for the app
    /// </summary>
    public int FramesPerSecond => layoutRoot.FramesPerSecond;

    /// <summary>
    /// Gets the total number of times a paint actually happened
    /// </summary>
    public int TotalPaints => layoutRoot.TotalPaints;
 

    /// <summary>
    /// Gets the bitmap that will be painted to the console
    /// </summary>
    public ConsoleBitmap Bitmap => LayoutRoot.Bitmap;

    /// <summary>
    /// Gets the root panel that contains the controls being used by the app
    /// </summary>
    public ConsolePanel LayoutRoot => layoutRoot;

    /// <summary>
    /// An event that fires just after painting the app
    /// </summary>
    public Event AfterPaint => layoutRoot.AfterPaint;

    /// <summary>
    /// Gets or sets the sound provider for this application. Note that klooie.Windows (a separate package)
    /// is required to actually have sound on Windows.
    /// </summary>
    public ISoundProvider Sound { get; set; } = new NoOpSoundProvider();

    public ConsoleApp()
    {
        Invoke(Startup);
    }

    /// <summary>
    /// Asserts that the current thread is running a ConsoleApp
    /// </summary>
    /// <param name="expectedApp">The specific app that is expected to be running on this thread or null to just check that any app is running</param>
    public static void AssertAppThread(ConsoleApp expectedApp = null)
    {
        if (Current == null)
        {
            throw new InvalidOperationException("There is no ConsoleApp running on this thread");
        }
        else if (expectedApp != null && Current != expectedApp)
        {
            throw new InvalidOperationException("The ConsoleApp on this thread is different from the one expected");
        }
    }

    public override void Run()
    {
        try
        {
            _current = this;
            Name = GetType().Name;
            layoutRoot = new LayoutRootPanel() { FocusStackDepth = 1 };
            OnDisposed(LayoutRoot.Dispose);
            focus = new FocusManager();
            base.Run();
        }
        finally
        {
            TryDispose();
            _current = null;
        }
    }

    protected virtual Task Startup() => Task.CompletedTask;

    public Task RequestPaintAsync() => layoutRoot == null ? Task.CompletedTask : layoutRoot.RequestPaintAsync();
    public void RequestPaint() => layoutRoot?.RequestPaint();


    public void MoveFocus(bool forward = true) => focus.MoveFocus(forward);
    public void SetFocus(ConsoleControl c) => focus.SetFocus(c);
    public void ClearFocus() => focus.ClearFocus();
    public void PushKeyForLifetime(ConsoleKey k, ConsoleModifiers? modifier, Action a, ILifetimeManager lt) => focus.GlobalKeyHandlers.PushForLifetime(k, modifier, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, Action a, ILifetimeManager lt) => focus.GlobalKeyHandlers.PushForLifetime(k, null, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, ConsoleModifiers modifier, Action a, ILifetimeManager lt, int stackIndex) => focus.Stack[stackIndex].Interceptors.PushForLifetime(k, modifier, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, Action a, ILifetimeManager lt, int stackIndex) => focus.Stack[stackIndex].Interceptors.PushForLifetime(k, null, a, lt);
    public Task SendKey(ConsoleKeyInfo key) => focus.SendKey(key);
    public Task SendKey(ConsoleKey key, bool shift = false, bool alt = false, bool control = false) => SendKey(key.KeyInfo(shift, alt, control));
}

