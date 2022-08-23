using PowerArgs;
namespace klooie;

/// <summary>
/// A class representing a console application that uses a message pump to synchronize work on a UI thread
/// </summary>
public partial class ConsoleApp : EventLoop
{
    [ThreadStatic]
    private static ConsoleApp _current;


    private List<TaskCompletionSource<bool>> paintRequests = new List<TaskCompletionSource<bool>>();
    private FrameRateMeter paintRateMeter = new FrameRateMeter();
        
    private class KeyRequest
    {
        public ConsoleKeyInfo Info { get; set; }
        public TaskCompletionSource<bool> TaskSource { get; set; }
    }
    private Queue<KeyRequest> sendKeys = new Queue<KeyRequest>();
    private ConsoleCharacter defaultPen = new ConsoleCharacter(' ', null, DefaultColors.BackgroundColor);
    /// <summary>
    /// True by default. When true, discards key presses that come in too fast
    /// likely because the user is holding the key down. You can set the
    /// MinTimeBetweenKeyPresses property to suit your needs.
    /// </summary>
    public bool KeyThrottlingEnabled { get; set; } = true;

    /// <summary>
    /// When key throttling is enabled this lets you set the minimum time that must
    /// elapse before we forward a key press to the app, provided it is the same key
    /// that was most recently clicked.
    /// </summary>
    public TimeSpan MinTimeBetweenKeyPresses { get; set; } = TimeSpan.FromMilliseconds(35);

    /// <summary>
    /// If true, paint requests will be honored. Defaults to true.
    /// </summary>
    public bool PaintEnabled { get; set; } = true;

    /// <summary>
    /// If true, clears the console when the app exits. Defaults to true.
    /// </summary>
    public bool ClearOnExit { get; set; } = true;
    public Event OnKeyInputThrottled { get; private set; } = new Event();
    private ConsoleKey lastKey;
    private DateTime lastKeyPressTime = DateTime.MinValue;
    /// <summary>
    /// An event that fires when the console window has been resized by the user
    /// </summary>
    public Event WindowResized { get; private set; } = new Event();


    private int lastConsoleWidth, lastConsoleHeight;

    private List<IDisposable> timerHandles = new List<IDisposable>();

    private FrameRateMeter cycleRateMeter;

    /// <summary>
    /// Gets the total number of event loop cycles that have run
    /// </summary>
    public int TotalCycles => cycleRateMeter != null ? cycleRateMeter.TotalFrames : 0;


    /// <summary>
    /// Gets the current frame rate for the app
    /// </summary>
    public int CyclesPerSecond => cycleRateMeter != null ? cycleRateMeter.CurrentFPS : 0;

    /// <summary>
    /// Gets a reference to the current app running on this thread.  This will only be populated by the thread
    /// that is running the message pump (i.e. it will never be your main thread).
    /// </summary>
    public static ConsoleApp Current
    {
        get
        {
            return _current;
        }
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

    /// <summary>
    /// Gets the current paint rate for the app
    /// </summary>
    public int PaintRequestsProcessedPerSecond
    {
        get
        {
            return paintRateMeter != null ? paintRateMeter.CurrentFPS : 0;
        }
    }

    /// <summary>
    /// Gets the total number of times a paint actually happened
    /// </summary>
    public int TotalPaints => paintRateMeter != null ? paintRateMeter.TotalFrames : 0;


    /// <summary>
    /// The writer used to record the contents of the screen while the app
    /// is running. If not set then recording does not take place
    /// </summary>
    public ConsoleBitmapVideoWriter Recorder { get; set; }

    /// <summary>
    /// An event that fires when the application is about to stop, before the console is wiped
    /// </summary>
    public Event Stopping { get; private set; } = new Event();

    /// <summary>
    /// An event that fires after the message pump is completely stopped and the console is wiped
    /// </summary>
    public Event Stopped { get; private set; } = new Event();

    /// <summary>
    /// An event that fires when a control is added to the visual tree
    /// </summary>
    public Event<ConsoleControl> ControlAdded { get; private set; } = new Event<ConsoleControl>();

    /// <summary>
    /// An event that fires when a control is removed from the visual tree
    /// </summary>
    public Event<ConsoleControl> ControlRemoved { get; private set; } = new Event<ConsoleControl>();

    /// <summary>
    /// Gets the bitmap that will be painted to the console
    /// </summary>
    public ConsoleBitmap Bitmap => LayoutRoot.Bitmap;

    /// <summary>
    /// Gets the root panel that contains the controls being used by the app
    /// </summary>
    public ConsolePanel LayoutRoot { get; private set; }

    private FocusManager Focus { get; set; }

    /// <summary>
    /// Gets or set whether or not to give focus to a control when the app starts.  The default is true.
    /// </summary>
    public bool SetFocusOnStart { get; set; }

    /// <summary>
    /// An event that fires just after painting the app
    /// </summary>
    public Event AfterPaint { get; private set; } = new Event();


        
    private TextWriter consoleWriter;


    /// <summary>
    /// Creates a new console app given a set of boundaries
    /// </summary>
    /// <param name="w">The width of the app</param>
    /// <param name="h">The height of the app</param>
    public ConsoleApp(int w, int h)
    {
        this.Name = GetType().Name;
 
        consoleWriter = Console.Out;
        this.lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
        this.lastConsoleHeight = ConsoleProvider.Current.WindowHeight;

        cycleRateMeter = new FrameRateMeter();

        this.EndOfCycle.SubscribeForLifetime(Cycle, this);
        SetFocusOnStart = true;
        LayoutRoot = new ConsolePanel(w,h);
        Focus = new FocusManager();
        LayoutRoot.Application = this;
        Focus.SubscribeForLifetime(nameof(Focus.StackDepth), () => FocusStackDepthChanged.Fire(Focus.StackDepth), this);
        Focus.SubscribeForLifetime(nameof(Focus.FocusedControl), () => FocusChanged.Fire(Focus.FocusedControl), this);
        Focus.SubscribeForLifetime(nameof(Focus.FocusedControl), () => RequestPaintAsync(), this);
        LayoutRoot.Controls.BeforeAdded.SubscribeForLifetime((c) => { c.Application = this; c.BeforeAddedToVisualTreeInternal(); }, this);
        LayoutRoot.Controls.BeforeRemoved.SubscribeForLifetime((c) => { c.BeforeRemovedFromVisualTreeInternal(); }, this);
        LayoutRoot.Controls.Added.SubscribeForLifetime(ControlAddedToVisualTree, this);
        LayoutRoot.Controls.Removed.SubscribeForLifetime(ControlRemovedFromVisualTree, this);
        WindowResized.SubscribeForLifetime(HandleDebouncedResize, this);
        this.LoopStarted.SubscribeOnce(() => _current = this);
        this.EndOfCycle.SubscribeForLifetime(DrainPaints, this);
    }

    public Event<int> FocusStackDepthChanged { get; private set; } = new Event<int>();
    public Event<ConsoleControl> FocusChanged { get; private set; } = new Event<ConsoleControl>();
    public void MoveFocus(bool forward = true) => Focus.MoveFocus(true);
    public void SetFocus(ConsoleControl c) => Focus.SetFocus(c);
    public void ClearFocus() => Focus.ClearFocus();
    public ConsoleControl FocusedControl => Focus.FocusedControl;
    public int FocusStackDepth => Focus.StackDepth;
    public void PushFocusStack() => Focus.Push();
    public void PopFocusStack() => Focus.Pop();

    public void PushKeyForLifetime(ConsoleKey k, ConsoleModifiers? modifier, Action a, ILifetimeManager lt) => Focus.GlobalKeyHandlers.PushForLifetime(k, modifier, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, Action a, ILifetimeManager lt) => Focus.GlobalKeyHandlers.PushForLifetime(k, null, a, lt);

    public void PushKeyForLifetime(ConsoleKey k, ConsoleModifiers modifier, Action a, ILifetimeManager lt, int stackIndex) => Focus.Stack.ToArray()[stackIndex].Interceptors.PushForLifetime(k, modifier, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, Action a, ILifetimeManager lt, int stackIndex) => Focus.Stack.ToArray()[stackIndex].Interceptors.PushForLifetime(k, null, a, lt);


    /// <summary>
    /// Writes the object as a ToString() to the debug output which can be seen if DebugEnabled is true and
    /// the user presses SHIFT+ALT+D.
    /// </summary>
    /// <param name="o">the object to stringify</param>
    public static void Debug(object o) => Debug(o?.ToString());

    /// <summary>
    /// Writes the object as a ToString() plus a newline to the debug output which can be seen if DebugEnabled is true and
    /// the user presses SHIFT+ALT+D.
    /// </summary>
    /// <param name="o">the object to stringify</param>
    public static void DebugLine(object o) => DebugLine(o?.ToString());

    private bool paintRequested;
    private void DrainPaints()
    {
        if (paintRequests.Count > 0)
        {
            PaintInternal();

            TaskCompletionSource<bool>[] paintRequestsCopy;               
            paintRequestsCopy = paintRequests.ToArray();
            paintRequests.Clear();
                                
            for(var i = 0; i < paintRequestsCopy.Length; i++)
            {
                paintRequestsCopy[i].SetResult(true);
            }
               
            paintRateMeter.Increment();
            paintRequested = false;
        }
        else if(paintRequested)
        {
            PaintInternal();
            paintRequested = false;
        }
    }

    /// <summary>
    /// Creates a full screen console app that will automatically adjust its layout if the window size changes
    /// </summary>
    public ConsoleApp(Action init = null) : this(ConsoleProvider.Current.BufferWidth, ConsoleProvider.Current.WindowHeight - 1)
    {
        if (init != null)
        {
            Invoke(init);
        }
    }

    /// <summary>
    /// Adds the given control to a ConsoleApp, fills the space, and blocks until the app terminates
    /// </summary>
    /// <param name="control">the control to show</param>
    public static void Show(ConsoleControl control)
    {
        var app = new ConsoleApp();
        app.LayoutRoot.Add(control).Fill();
        app.Start().Wait();
    }

    /// <summary>
    /// Starts a new ConsoleApp and waits for it to finish
    /// </summary>
    /// <param name="init">the function that initializes the app</param>
    public static void Show(Action<ConsoleApp> init)
    {
        var app = new ConsoleApp();
        app.InvokeNextCycle(() => init(app));
        app.Start().Wait();
    }



    /// <summary>
    /// Starts the app, asynchronously.
    /// </summary>
    /// <returns>A task that will complete when the app exits</returns>
    public override async Task Start()
    {
        if (SetFocusOnStart)
        {
            InvokeNextCycle(() => Focus.MoveFocus());
        }

        try
        {
            Invoke(Startup);
            await base.Start();
        }
        finally
        {
            ExitInternal();
        }
    }

    public override void Run()
    {
        _current = this;
        if (SetFocusOnStart)
        {
            InvokeNextCycle(() => Focus.MoveFocus());
        }

        try
        {
            Invoke(Startup);
            base.Run();
        }
        finally
        {
            ExitInternal();
        }
    }

    protected virtual Task Startup() => Task.CompletedTask;

    private void HandleDebouncedResize()
    {
        if (Bitmap.Console.BufferWidth < 1 || Bitmap.Console.WindowHeight - 1 < 1)
        {
            return;
        }

        this.LayoutRoot.Width = Bitmap.Console.BufferWidth;
        this.LayoutRoot.Height = Bitmap.Console.WindowHeight - 1;
        RequestPaint();
    }

    /// <summary>
    /// Queues up a request to paint the app.  The system will dedupe multiple paint requests when there are multiple in the pump's work queue
    /// <returns>a Task that resolves after the paint happens</returns>
    /// </summary>
    public Task RequestPaintAsync()
    {
        if (IsDrainingOrDrained) return Task.CompletedTask;
        AssertAppThread(this);
        var d = new TaskCompletionSource<bool>();
        paintRequests.Add(d);
        return d.Task;
    }

    public void RequestPaint()
    {
        paintRequested = true;
    }

    private void ControlAddedToVisualTree(ConsoleControl c)
    {
        c.Application = this;
        c.OnDisposed(() =>
        {
            if (c.Application == this && c.Parent != null && c.Parent.Application == this)
            {
                if (c.Parent is ConsolePanel)
                {
                    (c.Parent as ConsolePanel).Controls.Remove(c);
                }
                else
                {
                    throw new NotSupportedException($"You cannot manually dispose child controls of parent type {c.Parent.GetType().Name}");
                }
            }
        });

        if (c is ConsolePanel)
        {
            var childPanel = c as ConsolePanel;
            childPanel.Controls.SynchronizeForLifetime((cp) => { ControlAddedToVisualTree(cp); }, (cp) => { ControlRemovedFromVisualTree(cp); }, () => { }, c);
        }
        else if (c is ProtectedConsolePanel)
        {
            var childPanel = c as ProtectedConsolePanel;
            ControlAddedToVisualTree(childPanel.ProtectedPanelInternal);
            childPanel.OnDisposed(() => ControlRemovedFromVisualTree(childPanel.ProtectedPanelInternal));
        }

        Focus.Add(c);
        c.AddedToVisualTreeInternal();

        ControlAdded.Fire(c);
    }

    private void ControlRemovedFromVisualTree(ConsoleControl c)
    {
        c.IsBeingRemoved = true;
        if (ControlRemovedFromVisualTreeRecursive(c))
        {
            Focus.RestoreFocus();
        }
    }

    private bool ControlRemovedFromVisualTreeRecursive(ConsoleControl c)
    {
        bool focusChanged = false;

        if (c is ConsolePanel)
        {
            foreach (var child in (c as ConsolePanel).Controls.ToArray())
            {
                child.IsBeingRemoved = true;
                focusChanged = ControlRemovedFromVisualTreeRecursive(child) || focusChanged;
            }
        }

        if (Focus.FocusedControl == c)
        {
            Focus.ClearFocus();
            focusChanged = true;
        }

        Focus.Remove(c);

        c.RemovedFromVisualTreeInternal();
        c.Application = null;
        ControlRemoved.Fire(c);
        if (c.IsExpired == false && c.IsExpiring == false)
        {
            c.Dispose();
        }
        return focusChanged;
    }

    /// <summary>
    /// Handles key input for the application
    /// </summary>
    /// <param name="info">The key that was pressed</param>
    protected virtual void HandleKeyInput(ConsoleKeyInfo info)
    {
        if (Focus.GlobalKeyHandlers.TryIntercept(info))
        {
            // great, it was handled
        }
        else if (info.Key == ConsoleKey.Tab)
        {
            Focus.MoveFocus(info.Modifiers.HasFlag(ConsoleModifiers.Shift) == false);
        }
        else if (info.Key == ConsoleKey.Escape)
        {
            Stop();
            return;
        }
        else if (Focus.FocusedControl != null)
        {
            if (Focus.FocusedControl.IsExpired == false)
            {
                Focus.FocusedControl.HandleKeyInput(info);
            }
        }
        else
        {
            // not handled
        }
        RequestPaint();
    }

    private void ExitInternal()
    {
        Stopping.Fire();
        Recorder?.WriteFrame(Bitmap, true);
        Recorder?.Finish();

        if (ClearOnExit)
        {
            ConsoleProvider.Current.Clear();
        }        
        Bitmap.Console.ForegroundColor = ConsoleString.DefaultForegroundColor;
        Bitmap.Console.BackgroundColor = ConsoleString.DefaultBackgroundColor;
        LayoutRoot.Dispose();
        Stopped.Fire();
        Dispose();
        _current = null;
    }


    private void PaintInternal()
    {
        Bitmap.Fill(defaultPen);
        LayoutRoot.Paint();

        Recorder?.WriteFrame(Bitmap);
        if (PaintEnabled)
        {
            Bitmap.Paint();
        }
        AfterPaint.Fire();
    }


    private void Cycle()
    {
        cycleRateMeter.Increment();
        // todo - if evaluation showed up on a profile. Consider checking this at most twice per second.
        if ((lastConsoleWidth != ConsoleProvider.Current.BufferWidth || lastConsoleHeight != ConsoleProvider.Current.WindowHeight))
        {
            DebounceResize();
            WindowResized.Fire();
        }

        if (ConsoleProvider.Current.KeyAvailable)
        {
            var info = ConsoleProvider.Current.ReadKey(true);

            var effectiveMinTimeBetweenKeyPresses = MinTimeBetweenKeyPresses;
            if (KeyThrottlingEnabled && info.Key == lastKey && DateTime.UtcNow - lastKeyPressTime < effectiveMinTimeBetweenKeyPresses)
            {
                // the user is holding the key down and throttling is enabled
                OnKeyInputThrottled.Fire();
            }
            else
            {
                lastKeyPressTime = DateTime.UtcNow;
                lastKey = info.Key;
                InvokeNextCycle(() => HandleKeyInput(info));
            }
        }
            
        if (sendKeys.Count > 0)
        {
            var request = sendKeys.Dequeue();
            InvokeNextCycle(() =>
            {
                HandleKeyInput(request.Info);
                request.TaskSource.SetResult(true);
            });
        }
    }



    /// <summary>
    /// Simulates a key press
    /// </summary>
    /// <param name="key">the key press info</param>
    public Task SendKey(ConsoleKeyInfo key)
    {
        var tcs = new TaskCompletionSource<bool>();
        Invoke(() => 
        {
            sendKeys.Enqueue(new KeyRequest() { Info = key, TaskSource = tcs });
        });
        return tcs.Task;
    }


    /// <summary>
    /// Schedules the given action for periodic processing by the message pump
    /// </summary>
    /// <param name="a">The action to schedule for periodic processing</param>
    /// <param name="interval">the execution interval for the action</param>
    /// <returns>A handle that can be passed to ClearInterval if you want to cancel the work</returns>
    public SetIntervalHandle SetInterval(Action a, TimeSpan interval)
    {
        var handle = new SetIntervalHandle(interval);
        Invoke(async () =>
        {
            while (IsRunning && IsDrainingOrDrained == false && handle.IsExpired == false)
            {
                await Task.Delay(handle.Interval);
                a();
            }
        });
        return handle;
    }

    /// <summary>
    /// Updates a previously scheduled interval
    /// </summary>
    /// <param name="handle">the handle that was returned by a previous call to setInterval</param>
    /// <param name="newInterval">the new interval</param>
    public void ChangeInterval(SetIntervalHandle handle, TimeSpan newInterval)
    {
        handle.Interval = newInterval;
    }

    /// <summary>
    /// Schedules the given action for a one time execution after the given period elapses
    /// </summary>
    /// <param name="a">The action to schedule</param>
    /// <param name="period">the period of time to wait before executing the action</param>
    /// <returns></returns>
    public IDisposable SetTimeout(Action a, TimeSpan period)
    {
        var lt = new Lifetime();
        Invoke(async () =>
        {
            await Task.Delay(period);
            if (lt.ShouldContinue)
            {
                a();
            }
        });
        return lt;
    }

    private void DebounceResize()
    {
        ConsoleProvider.Current.Clear();
        bool done = false;
        var debouncer = new TimerActionDebouncer(TimeSpan.FromSeconds(.25), () =>
        {
            done = true;
        });

        debouncer.Trigger();
        while (done == false)
        {
            if (ConsoleProvider.Current.BufferWidth != lastConsoleWidth || ConsoleProvider.Current.WindowHeight != lastConsoleHeight)
            {
                lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
                lastConsoleHeight = ConsoleProvider.Current.WindowHeight;
                debouncer.Trigger();
            }
        }
    }

    public class SetIntervalHandle : Lifetime
    {
        internal TimeSpan Interval { get; set; }

        internal SetIntervalHandle(TimeSpan interval)
        {
            this.Interval = interval;
        }
    }
}

