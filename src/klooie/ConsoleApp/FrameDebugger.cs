using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;


public sealed class FrameDebugger
{
    private static FrameDebugger Instance { get; set; } 
    private readonly List<FrameInfo> _frames;
    private Dictionary<string, int> _currentTaskCounts;
    private long _currentFrameStart;
    private bool _frameInProgress = false;

    public struct FrameInfo
    {
        public long FrameStartTimestamp { get; set; }
        public double FrameDurationMs { get; set; }
        public Dictionary<string, int> TaskCounts { get; set; }
    }

    public static List<FrameInfo> Attach(ILifetime lt)
    {
        if (Instance != null) throw new InvalidOperationException("Frame debugging is already enabled for this app");
        Instance = new FrameDebugger(lt);
        lt.OnDisposed(static () => FrameDebugger.Instance = null);
        return Instance._frames;
    }

    private FrameDebugger(ILifetime lifetime)
    {
        _frames = new List<FrameInfo>();
        _currentTaskCounts = new Dictionary<string, int>();
        // Subscribe to the paint event for the given lifetime
        ConsoleApp.Current.AfterPaint.Subscribe(this, static(me)=> me.OnPaint(), lifetime);
        // Clean up at end of lifetime if frame in progress
        lifetime.OnDisposed(this, static(me) => me.OnEndOfLifetime());
    }

    private void OnEndOfLifetime()
    {
        if (_frameInProgress == false) return;
        EndFrame();
        _frameInProgress = false;
    }

    private void OnPaint()
    {
        if (_frameInProgress)
        {
            EndFrame();
        }
        BeginFrame();
        _frameInProgress = true;
    }

    private void BeginFrame()
    {
        _currentFrameStart = Stopwatch.GetTimestamp();
        _currentTaskCounts = new Dictionary<string, int>(97);
    }

    public static void RegisterTask(string name)
    {
        if (Instance == null) return;
        if(Instance._currentTaskCounts.TryGetValue(name, out var count))
        {
            Instance._currentTaskCounts[name] = count + 1;
        }
        else
        {
            Instance._currentTaskCounts[name] = 1;
        }
    }

    private void EndFrame()
    {
        var now = Stopwatch.GetTimestamp();
        var durationMs = (now - _currentFrameStart) * 1000.0 / Stopwatch.Frequency;


        var info = new FrameInfo
        {
            FrameStartTimestamp = _currentFrameStart,
            FrameDurationMs = durationMs,
            TaskCounts = _currentTaskCounts
        };
        _frames.Add(info);
    }
}

 
