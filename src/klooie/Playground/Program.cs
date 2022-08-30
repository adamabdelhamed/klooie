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
    [Benchmark]
    public void BenchmarkEvents()
    {
        var numberOfEvents = 5;
        var numberOfSubscribersPerEvent = 5;
        var numberOfFires = 5;

        var count = 0;
        for (var i = 0; i < numberOfEvents; i++)
        {
            using (var eventLt = new Lifetime())
            {
                var ev = new Event();
                for (var j = 0; j < numberOfSubscribersPerEvent; j++)
                {
                    ev.SubscribeForLifetime(() => count++, eventLt);
                }

                for (var k = 0; k < numberOfFires; k++)
                {
                    ev.Fire();
                }
            }
        }
    }
}

