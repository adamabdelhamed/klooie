using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using klooie;
using PowerArgs;


public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<EventBenchmark>();
    }
}

[MemoryDiagnoser]
public class EventBenchmark
{
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

