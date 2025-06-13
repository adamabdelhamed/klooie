using System;
using System.Diagnostics;

namespace klooie;
internal class FrameRateMeter
{
    private const int BufferSize = 3; // Rolling window of 3 seconds
    private int[] framesPerSecond = new int[BufferSize];
    private double[] slowestMsPerSecond = new double[BufferSize];
    private int[] slowFramesPerSecond = new int[BufferSize];
    private int bufferIndex = 0;

    private int framesThisSecond = 0;
    private double slowestThisSecond = 0;
    private int slowFramesThisSecond = 0;
    private long secondStartTimestamp;

    private long lastFrameTimestamp;
    private static readonly double TimestampToMs = 1000.0 / Stopwatch.Frequency;

    public double FrameThresholdMs { get; set; } = 20;
    public int TotalFrames { get; private set; }

    public FrameRateMeter()
    {
        var now = Stopwatch.GetTimestamp();
        lastFrameTimestamp = now;
        secondStartTimestamp = now;
    }

    public void Increment()
    {
        var now = Stopwatch.GetTimestamp();
        TotalFrames++;

        // Frame duration
        var frameMs = (now - lastFrameTimestamp) * TimestampToMs;
        lastFrameTimestamp = now;

        // Stats for current second
        framesThisSecond++;
        if (frameMs > slowestThisSecond)
            slowestThisSecond = frameMs;
        if (frameMs > FrameThresholdMs)
            slowFramesThisSecond++;

        // New second?
        if ((now - secondStartTimestamp) > Stopwatch.Frequency)
        {
            // Store current stats in buffer
            bufferIndex = (bufferIndex + 1) % BufferSize;
            framesPerSecond[bufferIndex] = framesThisSecond;
            slowestMsPerSecond[bufferIndex] = slowestThisSecond;
            slowFramesPerSecond[bufferIndex] = slowFramesThisSecond;

            // Reset for next second
            framesThisSecond = 0;
            slowestThisSecond = 0;
            slowFramesThisSecond = 0;
            secondStartTimestamp = now;
        }
    }

    public int CurrentFPS => (int)Math.Round(Sum(framesPerSecond) / (double)BufferSize);

    public int SlowestRecentFrame => (int)Math.Round(Max(slowestMsPerSecond));

  
    private int Sum(int[] arr) { int sum = 0; foreach (var x in arr) sum += x; return sum; }
    private double Sum(double[] arr) { double sum = 0; foreach (var x in arr) sum += x; return sum; }
    private double Max(double[] arr) { double max = 0; foreach (var x in arr) if (x > max) max = x; return max; }
}
