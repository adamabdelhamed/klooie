using System.Diagnostics;

namespace klooie;

/// <summary>
/// A class representing a console application that uses a message pump to synchronize work on a UI thread
/// </summary>
public partial class ConsoleApp : EventLoop
{
    [ThreadStatic]
    private static ConsoleApp _current;

    /// <summary>
    /// Gets a reference to the current app running on this thread.  This will only be populated by the thread
    /// that is running the message pump (i.e. it will never be your main thread).
    /// </summary>
    public static ConsoleApp Current => _current;

    private List<TaskCompletionSource<bool>> paintRequests = new List<TaskCompletionSource<bool>>();
    private FrameRateMeter paintRateMeter = new FrameRateMeter();
    private Queue<KeyRequest> sendKeys = new Queue<KeyRequest>();
    private ConsoleCharacter defaultPen = new ConsoleCharacter(' ', null, DefaultColors.BackgroundColor);
    private ConsoleKey lastKey;
    private DateTime lastKeyPressTime = DateTime.MinValue;
    private FrameRateMeter cycleRateMeter;
    private int lastConsoleWidth, lastConsoleHeight;
    private FocusManager focus;
    private bool paintRequested;

    /// <summary>
    /// Gets the width of the app
    /// </summary>
    public int Width => LayoutRoot.Width;
    /// <summary>
    /// Gets the height of the app
    /// </summary>
    public int Height => LayoutRoot.Height;
    /// <summary>
    /// When key throttling is enabled this lets you set the minimum time that must
    /// elapse before we forward a key press to the app, provided it is the same key
    /// that was most recently pressed.
    /// </summary>
    public TimeSpan MinTimeBetweenKeyPresses { get; set; } = TimeSpan.FromMilliseconds(35);
    /// <summary>
    /// True by default. When true, discards key presses that come in too fast
    /// likely because the user is holding the key down. You can set the
    /// MinTimeBetweenKeyPresses property to suit your needs.
    /// </summary>
    public bool KeyThrottlingEnabled { get; set; } = true;

    /// <summary>
    /// An event that fires when key input has been throttled. Only fired
    /// when KeyThrottlingEnabled is true.
    /// </summary>
    public Event OnKeyInputThrottled { get; private set; } = new Event();

    /// <summary>
    /// An event that fires when the user resizes the window. The Bounds of
    /// LayoutRoot will be updated before this event fires.
    /// </summary>
    public Event OnWindowResized { get; private init; } = new Event();


    /// <summary>
    /// An event that fires whenever a key is pressed. Other handlers will also run.
    /// </summary>
    public Event<ConsoleKeyInfo> GlobalKeyPressed { get; private set; } = new Event<ConsoleKeyInfo>();

    /// <summary>
    /// If true, paint requests will be honored. Defaults to true.
    /// </summary>
    public bool PaintEnabled { get; set; } = true;

    /// <summary>
    /// If true, clears the console when the app exits. Defaults to true.
    /// </summary>
    public bool ClearOnExit { get; set; } = true;

    /// <summary>
    /// Gets the total number of event loop cycles that have run
    /// </summary>
    public int TotalCycles => cycleRateMeter != null ? cycleRateMeter.TotalFrames : 0;

    /// <summary>
    /// Gets the current frame rate for the app
    /// </summary>
    public int CyclesPerSecond => cycleRateMeter != null ? cycleRateMeter.CurrentFPS : 0;

    /// <summary>
    /// Gets the current paint rate for the app
    /// </summary>
    public int FramesPerSecond => paintRateMeter != null ? paintRateMeter.CurrentFPS : 0;

    /// <summary>
    /// Gets the total number of times a paint actually happened
    /// </summary>
    public int TotalPaints => paintRateMeter != null ? paintRateMeter.TotalFrames : 0;

    /// <summary>
    /// An event that fires when the application is about to stop, before the console is wiped
    /// </summary>
    public Event Stopping { get; private set; } = new Event();

    /// <summary>
    /// An event that fires after the message pump is completely stopped and the console is wiped
    /// </summary>
    public Event Stopped { get; private set; } = new Event();

    /// <summary>
    /// Gets the bitmap that will be painted to the console
    /// </summary>
    public ConsoleBitmap Bitmap => LayoutRoot.Bitmap;

    /// <summary>
    /// Gets the root panel that contains the controls being used by the app
    /// </summary>
    public ConsolePanel LayoutRoot { get; private set; }


    /// <summary>
    /// An event that fires just after painting the app
    /// </summary>
    public Event AfterPaint { get; private set; } = new Event();

    /// <summary>
    /// An event that fires when the current focus stack depth changes
    /// </summary>
    public Event<int> FocusStackDepthChanged { get; private set; } = new Event<int>();

    /// <summary>
    /// An event that fires when the focused control changes
    /// </summary>
    public Event<ConsoleControl> FocusChanged { get; private set; } = new Event<ConsoleControl>();

    /// <summary>
    /// Gets the current focused control
    /// </summary>
    public ConsoleControl FocusedControl => focus.FocusedControl;

    /// <summary>
    /// Gets the current focus stack depth
    /// </summary>
    public int FocusStackDepth => focus.StackDepth;

    /// <summary>
    /// Gets or sets the sound provider for this application. Note that klooie.Windows (a separate package)
    /// is required to actually have sound on Windows.
    /// </summary>
    public ISoundProvider Sound { get; set; } = new NoOpSoundProvider();

    /// <summary>
    /// Creates a new console app given a set of boundaries
    /// </summary>
    public ConsoleApp()
    {
        _current = this;
        Name = GetType().Name;
        lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
        lastConsoleHeight = ConsoleProvider.Current.WindowHeight - 1;
        cycleRateMeter = new FrameRateMeter();
        EndOfCycle.Subscribe(Cycle, this);
        LayoutRoot = new LayoutRootPanel() { FocusStackDepth = 1 };
        LayoutRoot.ResizeTo(lastConsoleWidth, lastConsoleHeight);
        focus = new FocusManager();
        focus.StackDepthChanged.Subscribe(() => FocusStackDepthChanged.Fire(focus.StackDepth), this);
        focus.FocusedControlChanged.Subscribe(() => FocusChanged.Fire(focus.FocusedControl), this);
        focus.FocusedControlChanged.Subscribe(() => RequestPaintAsync(), this);
        EndOfCycle.Subscribe(DrainPaints, this);
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
            base.Run();
        }
        finally
        {
            ExitInternal();
        }
    }

    protected virtual Task Startup() => Task.CompletedTask;

    public void MoveFocus(bool forward = true) => focus.MoveFocus(forward);
    public void SetFocus(ConsoleControl c) => focus.SetFocus(c);
    public void ClearFocus() => focus.ClearFocus();

    public void PushKeyForLifetime(ConsoleKey k, ConsoleModifiers? modifier, Action a, ILifetimeManager lt) => focus.GlobalKeyHandlers.PushForLifetime(k, modifier, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, Action a, ILifetimeManager lt) => focus.GlobalKeyHandlers.PushForLifetime(k, null, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, ConsoleModifiers modifier, Action a, ILifetimeManager lt, int stackIndex) => focus.Stack[stackIndex].Interceptors.PushForLifetime(k, modifier, a, lt);
    public void PushKeyForLifetime(ConsoleKey k, Action a, ILifetimeManager lt, int stackIndex) => focus.Stack[stackIndex].Interceptors.PushForLifetime(k, null, a, lt);
 



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
    /// simulates a key press
    /// </summary>
    /// <param name="key">The key that was pressed</param>
    /// <param name="shift">was shift pressed</param>
    /// <param name="alt">was alt pressed</param>
    /// <param name="control">was control pressed</param>
    /// <returns></returns>
    public Task SendKey(ConsoleKey key, bool shift = false, bool alt = false, bool control = false) => SendKey(key.KeyInfo(shift, alt, control));


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

    private void DrainPaints()
    {
        if (paintRequests.None() && paintRequested == false) return;

        PaintInternal();

        if (paintRequests.None()) return;

        TaskCompletionSource<bool>[] paintRequestsCopy;
        paintRequestsCopy = paintRequests.ToArray();
        paintRequests.Clear();

        for (var i = 0; i < paintRequestsCopy.Length; i++)
        {
            paintRequestsCopy[i].SetResult(true);
        }
    }




    private void HandleKeyInput(ConsoleKeyInfo info)
    {
        GlobalKeyPressed.Fire(info);

        if (focus.GlobalKeyHandlers.TryIntercept(info))
        {
            // great, it was handled
        }
        else if (info.Key == ConsoleKey.Tab)
        {
            focus.MoveFocus(info.Modifiers.HasFlag(ConsoleModifiers.Shift) == false);
        }
        else if (info.Key == ConsoleKey.Escape)
        {
            Stop();
            return;
        }
        else if (focus.FocusedControl != null)
        {
            if (focus.FocusedControl.IsExpired == false)
            {
                focus.FocusedControl.HandleKeyInput(info);
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
        try
        {
            Stopping.Fire();
            OnDisposed(() =>
            {
                LayoutRoot.Dispose();
                Stopped.Fire();
            });
            Dispose();
            _current = null;
        }
        finally
        {
            ConsoleProvider.Current.ForegroundColor = ConsoleString.DefaultForegroundColor;
            ConsoleProvider.Current.BackgroundColor = ConsoleString.DefaultBackgroundColor;
            if (ClearOnExit)
            {
                ConsoleProvider.Current.Clear();
            }
        }
    }

    private void PaintInternal()
    {
        paintRequested = false;
        Bitmap.Fill(defaultPen);
        LayoutRoot.Paint();

        if (PaintEnabled)
        {
            Bitmap.Paint();
        }
        paintRateMeter.Increment();
        AfterPaint.Fire();
    }

    private static readonly long CycleThrottlerIntervalTicks = Stopwatch.Frequency / 1000 * 25; // 25ms in ticks
    private long lastCycleThrottlerCheck;

    private void Cycle()
    {
        cycleRateMeter.Increment();

        // Check if enough time has passed since the last key check
        var delta = Stopwatch.GetTimestamp() - lastCycleThrottlerCheck;
        if (delta >= CycleThrottlerIntervalTicks)
        {
            DebounceResize();
            lastCycleThrottlerCheck = Stopwatch.GetTimestamp();

            if (ConsoleProvider.Current.KeyAvailable)
            {
                var info = ConsoleProvider.Current.ReadKey(true);

                var effectiveMinTimeBetweenKeyPresses = MinTimeBetweenKeyPresses;
                if (KeyThrottlingEnabled && info.Key == lastKey && DateTime.UtcNow - lastKeyPressTime < effectiveMinTimeBetweenKeyPresses)
                {
                    // The user is holding the key down and throttling is enabled
                    OnKeyInputThrottled.Fire();
                }
                else
                {
                    lastKeyPressTime = DateTime.UtcNow;
                    lastKey = info.Key;
                    InvokeNextCycle(() => HandleKeyInput(info));
                }
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


    private void DebounceResize()
    {
        if (lastConsoleWidth == ConsoleProvider.Current.BufferWidth && lastConsoleHeight == ConsoleProvider.Current.WindowHeight - 1)return;
        ConsoleProvider.Current.Clear();

        var lastSyncTime = DateTime.UtcNow;
        while (DateTime.UtcNow - lastSyncTime > TimeSpan.FromSeconds(.25f) == false)
        {
            if (ConsoleProvider.Current.BufferWidth != lastConsoleWidth || ConsoleProvider.Current.WindowHeight - 1 != lastConsoleHeight)
            {
                lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
                lastConsoleHeight = ConsoleProvider.Current.WindowHeight - 1;
                lastSyncTime = DateTime.UtcNow;
            }
        }

        if (Bitmap.Console.BufferWidth < 1 || Bitmap.Console.WindowHeight - 1 < 1)
        {
            return;
        }

        this.LayoutRoot.Width = Bitmap.Console.BufferWidth;
        this.LayoutRoot.Height = Bitmap.Console.WindowHeight - 1;
        RequestPaint();
        OnWindowResized.Fire();
    }

    public class SetIntervalHandle : Lifetime
    {
        internal TimeSpan Interval { get; set; }

        internal SetIntervalHandle(TimeSpan interval)
        {
            this.Interval = interval;
        }
    }

    private class KeyRequest
    {
        public ConsoleKeyInfo Info { get; set; }
        public TaskCompletionSource<bool> TaskSource { get; set; }
    }
}

internal class FrameRateMeter
{
    private DateTime start;
    private DateTime currentSecond;
    private int framesInCurrentSecond;
    private int framesInPreviousSecond;

    public int TotalFrames { get; private set; }
    public int CurrentFPS => framesInPreviousSecond;

    public FrameRateMeter()
    {
        start = DateTime.UtcNow;
        currentSecond = start;
        framesInCurrentSecond = 0;
    }

    public void Increment()
    {
        var now = DateTime.UtcNow;
        TotalFrames++;

        if (AreSameSecond(now, currentSecond))
        {
            framesInCurrentSecond++;
        }
        else
        {
            framesInPreviousSecond = framesInCurrentSecond;
            framesInCurrentSecond = 0;
            currentSecond = now;
        }
    }

    private bool AreSameSecond(DateTime a, DateTime b) =>
        a.Year == b.Year &&
        a.Month == b.Month &&
        a.Day == b.Day &&
        a.Hour == b.Hour &&
        a.Minute == b.Minute &&
        a.Second == b.Second;
}

public class LayoutRootPanel : ConsolePanel
{
    public LayoutRootPanel()
    {
        // Subscribe once in the constructor
        Controls.Added.Subscribe(ControlAddedToVisualTree, this);
        Controls.Removed.Subscribe(ControlRemovedFromVisualTree, this);
    }

    private void ControlAddedToVisualTree(ConsoleControl c)
    {
        c.BeforeAddedToVisualTreeInternal();

        if (c is ConsolePanel childPanel)
        {
            // Synchronize child controls without causing multiple subscriptions
            childPanel.Controls.Sync(ControlAddedToVisualTree, ControlRemovedFromVisualTree, null, c);
        }
        else if (c is ProtectedConsolePanel protectedPanel)
        {
            // Handle protected panels
            ControlAddedToVisualTree(protectedPanel.ProtectedPanelInternal);
            protectedPanel.OnDisposed(() => ControlRemovedFromVisualTree(protectedPanel.ProtectedPanelInternal));
        }
        DescendentAdded.Fire(c);
        c.AddedToVisualTreeInternal();
   
        ConsoleApp.Current.RequestPaint();
    }

    private void ControlRemovedFromVisualTree(ConsoleControl c)
    {
        if (c.IsBeingRemoved)
            return; // Prevent re-entrancy

        c.IsBeingRemoved = true;
        ControlRemovedFromVisualTreeRecursive(c);
        ConsoleApp.Current.RequestPaint();
    }

    private void ControlRemovedFromVisualTreeRecursive(ConsoleControl c)
    {
        c.BeforeRemovedFromVisualTreeInternal();

        if (c is ConsolePanel panel)
        {
            // Iterate over a copy to prevent modification during iteration
            foreach (var child in panel.Controls.ToArray())
            {
                ControlRemovedFromVisualTree(child);
            }
        }
        else if (c is ProtectedConsolePanel protectedPanel)
        {
            ControlRemovedFromVisualTree(protectedPanel.ProtectedPanelInternal);
        }

        c.RemovedFromVisualTreeInternal();
        DescendentRemoved.Fire(c);

        if (c.ShouldContinue)
        {
            c.Dispose();
        }
    }
}