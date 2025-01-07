using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using klooie;

public class Program
{
    public static void Main(string[] args)
    {
      
    }
}

[MemoryDiagnoser]
public class DescendentsBenchmark
{
    ConsolePanel container;
    [GlobalSetup]
    public void Setup()
    {
        container = new ConsolePanel();
        for (var i = 0; i < 10; i++)
        {
            var child = container.Add(new ConsolePanel());
            for(var j = 0; j < 10; j++)
            {
                var grandchild = child.Add(new ConsoleControl());
            }
        }
    }

    [Benchmark]
    public void DescendentsFast()
    {
        var buffer = Container.DescendentBufferPool.Rent();
        container.PopulateDescendentsWithZeroAllocations(buffer);
        Container.DescendentBufferPool.Return(buffer);
    }

    [Benchmark]
    public void DescendentsSlow()
    {
        var d = container.Descendents;
    }
}


[MemoryDiagnoser]
public class PoolBenchmark
{
    SingleThreadObjectPool<object> singleThreadedPool = new SingleThreadObjectPool<object>();
    ConcurrentbjectPool<object> concurrentPool = new ConcurrentbjectPool<object>();

    private static Object o;
    [Benchmark]
    public void NewObject()
    {
         o = new object();
    }

    [Benchmark]
    public void SingleThreadedPoolRentalAndReturn()
    {
        o = singleThreadedPool.Rent();
        singleThreadedPool.Return(o);
    }

    [Benchmark]
    public void ConcurrentPoolRentalAndReturn()
    {
        o = concurrentPool.Rent();
        concurrentPool.Return(o);
    }
}