using System.Diagnostics;
namespace klooie;
public class EventLoopCycleThrottle
{
    private readonly double _maxCyclesPerSecond = 500;
    private readonly long minTicksBetweenCycles;
    private long lastCycleTime;
    public EventLoopCycleThrottle(EventLoop loop, int maxCyclesPerSecond = 500)
    {
        this._maxCyclesPerSecond = Math.Clamp(maxCyclesPerSecond, 1, 100_000);
        minTicksBetweenCycles = (long)(Stopwatch.Frequency / _maxCyclesPerSecond);
        lastCycleTime = Stopwatch.GetTimestamp();
        loop.EndOfCycle.Subscribe(this,Throttle, loop);
    }

    private static void Throttle(object me)
    {
        var _this = (EventLoopCycleThrottle)me;
        var now = Stopwatch.GetTimestamp();

        while (now - _this.lastCycleTime < _this.minTicksBetweenCycles)
        {
            var ticksRemaining = _this.minTicksBetweenCycles - (now - _this.lastCycleTime);
            var msRemaining = ticksRemaining * 1000.0 / Stopwatch.Frequency;
            if (msRemaining > 2.0)
            {
                Thread.Sleep(0);
            }
            else
            {
                Thread.Yield();
            }
            now = Stopwatch.GetTimestamp();
        }
        _this.lastCycleTime = now;
    }
}
