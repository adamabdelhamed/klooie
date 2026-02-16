using klooie.Gaming;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace klooie;

public partial class LayoutRootPanel : ConsolePanel
{
    public const int MaxPaintRate = 30;
    private Event _onWindowResized, _afterPaint;
    private int lastConsoleWidth, lastConsoleHeight;
    private List<TaskCompletionSource> paintRequests;
    private FrameRateMeter paintRateMeter;
    private ConsoleCharacter defaultPen;
    internal Event OnWindowResized { get => _onWindowResized ?? (_onWindowResized = Event.Create()); }

    public Event AfterPaint => _afterPaint ?? (_afterPaint = Event.Create());

    internal int FramesPerSecond => paintRateMeter.CurrentFPS;
    internal double SlowestRecentFrame => paintRateMeter.SlowestRecentFrame;
    internal double FastestRecentFrame => paintRateMeter.FastestRecentFrame;
    internal int TotalPaints => paintRateMeter.TotalFrames;
    internal bool PaintEnabled { get; set; } = true;
    internal bool ClearOnExit { get; set; } = true;

    public ITerminalHost TerminalHost { get; set; } 

    public LayoutRootPanel()
    {
    }

    protected override void OnInit()
    {
        base.OnInit();
        ConsoleApp.AssertAppThread();
        TerminalHost = new AnsiTerminalHost();
        defaultPen = new ConsoleCharacter(' ', null, DefaultColors.BackgroundColor);
        paintRequests = new List<TaskCompletionSource>();
        paintRateMeter = new FrameRateMeter();
        lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
        lastConsoleHeight = ConsoleProvider.Current.WindowHeight;
        ResizeTo(lastConsoleWidth, lastConsoleHeight);
        ConsoleApp.Current.EndOfCycle.SubscribeThrottled(this, static me => me.DrainPaints(), this, MaxPaintRate);
        DescendentAdded.Subscribe(this, static (me,added) => me.OnDescendentAdded(added), this);
        OnDisposed(RestoreConsoleState);
        FocusStackDepth = 1;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _onWindowResized?.TryDispose();
        _onWindowResized = null;
        _afterPaint?.TryDispose();
        _afterPaint = null;
    }

    private void OnDescendentAdded(ConsoleControl control) => control.AddedToVisualTreeInternal();
    
    private void RestoreConsoleState()
    {
        ConsoleProvider.Current.ForegroundColor = ConsoleString.DefaultForegroundColor;
        ConsoleProvider.Current.BackgroundColor = ConsoleString.DefaultBackgroundColor;
        if (ClearOnExit && PaintEnabled)
        {
            ConsoleProvider.Current.Clear();
        }
    }

    /// <summary>
    /// Queues up a request to paint the app.  The system will dedupe multiple paint requests when there are multiple in the pump's work queue
    /// <returns>a Task that resolves after the paint happens</returns>
    /// </summary>
    internal Task RequestPaintAsync()
    {
        ConsoleApp.AssertAppThread();
        if (ConsoleApp.Current!.IsDrainingOrDrained) return Task.CompletedTask;
        var d = new TaskCompletionSource();
        paintRequests.Add(d);
        return d.Task;
    }

    private void DrainPaints()
    {
        var resized = TerminalHost.SyncSize(this);
        if (resized) OnWindowResized.Fire();

        Bitmap.Fill(defaultPen);
        if (!PaintEnabled) return;

        Paint();

        // If host skipped (e.g. ConsolePainter throttling), skip rest of frame work.
        if (!TerminalHost.Present(this, Bitmap)) return;

        paintRateMeter.Increment();
        _afterPaint?.Fire();

        if (paintRequests.Count == 0) return;

        var paintRequestsCopy = paintRequests.ToArray();
        paintRequests.Clear();
        for (var i = 0; i < paintRequestsCopy.Length; i++) paintRequestsCopy[i].SetResult();
    }

    private static int NextControlId;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ConsoleControl, Box<int>> Ids = new();
    private sealed class Box<T> { public T Value; public Box(T v) { Value = v; } }
    public static int GetIdForPresentation(ConsoleControl c)
    {
        if (Ids.TryGetValue(c, out var box)) return box.Value;
        var id = System.Threading.Interlocked.Increment(ref NextControlId);
        Ids.Add(c, new Box<int>(id));
        return id;
    }
}




