using System.Diagnostics;
namespace klooie.Gaming;
public interface IStopwatch
{
    public TimeSpan Elapsed { get; }
    void Start();
    void Stop();
}

public class WallClockStopwatch : IStopwatch
{
    private Stopwatch sw = new Stopwatch();
    public TimeSpan Elapsed => sw.Elapsed;
    public void Start() => sw.Start();
    public void Stop() => sw.Stop();
}