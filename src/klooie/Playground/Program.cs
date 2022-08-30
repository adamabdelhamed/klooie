using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using klooie;
using PowerArgs;


public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<EventBenchmark>();
        return;
        var b = new EventBenchmark();
        b.Setup();
        for(var i = 0; i < 100000; i++)
        {
            b.BenchmarkFire();
        }
    }
}

[MemoryDiagnoser]
public class EventBenchmark
{
    public int Count => count;

    int numberOfEvents = 1;
    int numberOfSubscribersPerEvent = 100;
    int numberOfFires = 50;
    int count = 0;
    private Event[] events;

    [GlobalSetup]
    public void Setup()
    {
        events = new Event[numberOfEvents];
        for(var i = 0; i < numberOfEvents; i++)
        {
            events[i] = new Event();
            for (var j = 0; j < numberOfSubscribersPerEvent; j++)
            {
                events[i].SubscribeForLifetime(() => count++, Lifetime.Forever);
            }
        }
    }

    [Benchmark]
    public void BenchmarkFire()
    {
        for(var i = 0; i < events.Length; i++)
        {
            for (var j = 0; j < numberOfFires; j++)
            {
                events[i].Fire();
            }
        }
    }
}

