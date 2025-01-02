namespace klooie;

public class LayoutRootPanel : ConsolePanel
{
    private int lastConsoleWidth, lastConsoleHeight;
    private List<TaskCompletionSource> paintRequests = new List<TaskCompletionSource>();
    private FrameRateMeter paintRateMeter = new FrameRateMeter();
    private bool paintRequested;
    private ConsoleCharacter defaultPen = new ConsoleCharacter(' ', null, DefaultColors.BackgroundColor);

    private FrameRateMeter cycleRateMeter;

    internal Event OnWindowResized { get; private init; } = new Event();
    internal Event AfterPaint { get; private set; } = new Event();
    internal int TotalCycles => cycleRateMeter != null ? cycleRateMeter.TotalFrames : 0;
    internal int CyclesPerSecond => cycleRateMeter != null ? cycleRateMeter.CurrentFPS : 0;
    internal int FramesPerSecond => paintRateMeter != null ? paintRateMeter.CurrentFPS : 0;
    internal int TotalPaints => paintRateMeter != null ? paintRateMeter.TotalFrames : 0;
    internal bool PaintEnabled { get; set; } = true;
    internal bool ClearOnExit { get; set; } = true;





    public LayoutRootPanel()
    {
        lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
        lastConsoleHeight = ConsoleProvider.Current.WindowHeight - 1;
        cycleRateMeter = new FrameRateMeter();
 
        ResizeTo(lastConsoleWidth, lastConsoleHeight);
        ConsoleApp.Current.EndOfCycle.Subscribe(cycleRateMeter.Increment, this);
        ConsoleApp.Current.EndOfCycle.Subscribe(DebounceResize, this);
        ConsoleApp.Current.EndOfCycle.Subscribe(DrainPaints, this);
        DescendentAdded.Subscribe(OnDescendentAdded, this);
        OnDisposed(Cleanup);
    }

    private void OnDescendentAdded(ConsoleControl control)
    {
        control.AddedToVisualTreeInternal();
    }

    private void Cleanup()
    {
        ConsoleProvider.Current.ForegroundColor = ConsoleString.DefaultForegroundColor;
        ConsoleProvider.Current.BackgroundColor = ConsoleString.DefaultBackgroundColor;
        if (ClearOnExit)
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
        if (ConsoleApp.Current.IsDrainingOrDrained) return Task.CompletedTask;
        var d = new TaskCompletionSource();
        paintRequests.Add(d);
        return d.Task;
    }

    internal void RequestPaint()
    {
        paintRequested = true;
    }

    private void DrainPaints()
    {
        if (paintRequests.None() && paintRequested == false) return;

        paintRequested = false;
        Bitmap.Fill(defaultPen);
        Paint();// ConsoleControl.Paint() is called here

        if (PaintEnabled)
        {
            Bitmap.Paint();
        }
        paintRateMeter.Increment();
        AfterPaint.Fire();

        if (paintRequests.None()) return;

        TaskCompletionSource[] paintRequestsCopy;
        paintRequestsCopy = paintRequests.ToArray();
        paintRequests.Clear();

        for (var i = 0; i < paintRequestsCopy.Length; i++)
        {
            paintRequestsCopy[i].SetResult();
        }
    }

    private void DebounceResize()
    {
        if (lastConsoleWidth == ConsoleProvider.Current.BufferWidth && lastConsoleHeight == ConsoleProvider.Current.WindowHeight - 1) return;
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

        Width = Bitmap.Console.BufferWidth;
        Height = Bitmap.Console.WindowHeight - 1;
        RequestPaint();
        OnWindowResized.Fire();
    }

 
}