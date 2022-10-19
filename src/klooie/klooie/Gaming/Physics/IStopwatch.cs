using System.Diagnostics;
namespace klooie.Gaming;
public interface IStopwatch
{
    bool SupportsMaxDT { get; }
    public TimeSpan Elapsed { get; }
    void Start();
    void Stop();
}

public sealed class WallClockStopwatch : IStopwatch
{
    private Stopwatch sw = new Stopwatch();
    public TimeSpan Elapsed => sw.Elapsed;
    public void Start() => sw.Start();
    public void Stop() => sw.Stop();

    public bool SupportsMaxDT => true;
}