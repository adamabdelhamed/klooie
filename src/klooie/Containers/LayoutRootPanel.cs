﻿using System.Diagnostics;

namespace klooie;

public partial class LayoutRootPanel : ConsolePanel
{
    private Event _onWindowResized;
    private int lastConsoleWidth, lastConsoleHeight;
    private List<TaskCompletionSource> paintRequests;
    private FrameRateMeter paintRateMeter;
    private bool paintRequested;
    private ConsoleCharacter defaultPen;
    private FrameRateMeter cycleRateMeter;

    internal Event OnWindowResized { get => _onWindowResized ?? (_onWindowResized = EventPool.Instance.Rent()); }
    internal int TotalCycles => cycleRateMeter.TotalFrames;
    internal int CyclesPerSecond => cycleRateMeter.CurrentFPS;
    internal int FramesPerSecond => paintRateMeter.CurrentFPS;
    internal int TotalPaints => paintRateMeter.TotalFrames;
    internal bool PaintEnabled { get; set; } = true;
    internal bool ClearOnExit { get; set; } = true;

    public LayoutRootPanel()
    {
    }

    protected override void OnInit()
    {
        base.OnInit();
        ConsoleApp.AssertAppThread();
        defaultPen = new ConsoleCharacter(' ', null, DefaultColors.BackgroundColor);
        paintRequests = new List<TaskCompletionSource>();
        paintRateMeter = new FrameRateMeter();
        lastConsoleWidth = ConsoleProvider.Current.BufferWidth;
        lastConsoleHeight = ConsoleProvider.Current.WindowHeight - 1;
        cycleRateMeter = new FrameRateMeter();
        ResizeTo(lastConsoleWidth, lastConsoleHeight);
        ConsoleApp.Current!.EndOfCycle.Subscribe(OnEndOfCycle, this);
        DescendentAdded.Subscribe(OnDescendentAdded, this);
        OnDisposed(RestoreConsoleState);
        FocusStackDepth = 1;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _onWindowResized?.TryDispose();
        _onWindowResized = null;
    }

    private void OnEndOfCycle()
    {
        cycleRateMeter.Increment();
        DebounceResize();
        DrainPaints();
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

    internal void RequestPaint()
    {
        paintRequested = true;
    }

    private long lastPaintTime = Stopwatch.GetTimestamp();
    private const double MaxFramesPerSecond = 31;
    private const double minMillisecondsBetweenPaints = 1000.0 / MaxFramesPerSecond;
    private void DrainPaints()
    {
        if (paintRequests.None() && paintRequested == false) return;

        var elapsed = Stopwatch.GetElapsedTime(lastPaintTime);
        if (elapsed.TotalMilliseconds < minMillisecondsBetweenPaints) return;
        lastPaintTime = Stopwatch.GetTimestamp();

        paintRequested = false;
        Bitmap.Fill(defaultPen);
        Paint();// ConsoleControl.Paint() is called here

        if (PaintEnabled)
        {
            Bitmap.Paint();
        }
        paintRateMeter.Increment();

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