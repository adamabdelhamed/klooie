using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public enum FrameTaskId
{
    Wander,
    Vision,
    Count // Always last!
}

public sealed class FrameDebugger
{
    public struct FrameInfo
    {
        public long FrameStartTimestamp { get; set; }
        public double FrameDurationMs { get; set; }
        public int[] TaskCounts { get; set; }
    }


    private readonly List<FrameInfo> _frames;

    private int[] _currentTaskCounts;
    private long _currentFrameStart;
    private bool _frameInProgress = false;

    public List<FrameDebuggerFinalOutputFrame> Output { get; private set; }

    public FrameDebugger(ILifetime lifetime)
    {

        _frames = new List<FrameInfo>();
        _currentTaskCounts = new int[(int)FrameTaskId.Count];
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
        Output = _frames.Select(f => new FrameDebuggerFinalOutputFrame(f)).ToList();
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
        Array.Clear(_currentTaskCounts, 0, _currentTaskCounts.Length);
    }

    public void RegisterTask(FrameTaskId taskId) => _currentTaskCounts[(int)taskId]++;

    private void EndFrame()
    {
        var now = Stopwatch.GetTimestamp();
        var durationMs = (now - _currentFrameStart) * 1000.0 / Stopwatch.Frequency;


        var info = new FrameInfo
        {
            FrameStartTimestamp = _currentFrameStart,
            FrameDurationMs = durationMs,
            TaskCounts = (int[])_currentTaskCounts.Clone()
        };
        _frames.Add(info);
    }
}

public class FrameDebuggerFinalOutputFrame
{
    public long FrameStartTimestamp { get; set; }
    public double FrameDurationMs { get; set; }
    public Dictionary<string, int> TaskCounts { get; set; }
    public FrameDebuggerFinalOutputFrame(FrameDebugger.FrameInfo frame)
    {
        this.FrameDurationMs = frame.FrameDurationMs;
        this.FrameStartTimestamp = frame.FrameStartTimestamp;
        this.TaskCounts = new Dictionary<string, int>();
        for (int i = 0; i < frame.TaskCounts.Length; i++)
        {
            var taskId = (FrameTaskId)i;
            TaskCounts[taskId.ToString()] = frame.TaskCounts[i];
        }
    }
}
