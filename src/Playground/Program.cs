

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using klooie;


BenchmarkRunner.Run<EventBenchmark>();
/*
var b = new EventBenchmark();
b.Setup();
b.OldObservable();
b.NewObservable();
*/
[MemoryDiagnoser]
public class EventBenchmark
{
    
    private OldObservable oldObservable;
    private NewObservable newObservable;


    private int oldCount;
    private int newCount;
    [GlobalSetup]
    public void Setup()
    {
        oldObservable = new OldObservable();
        newObservable = new NewObservable();
    }

    public int GetCounts() => oldCount + newCount;
    

    [Benchmark(Baseline = true)]
    public void OldObservableTest()
    {
        oldObservable.Set<string>("",nameof(OldObservable.Name));
    }

    [Benchmark]
    public void NewObservableTest()
    {
 
    }
}

public class OldObservable : ObservableObject
{
    public string Name { get => Get<string>(); set => Set(value); }
}

public partial class NewObservable : IObservableObject
{
    public partial string Name { get; set; }
}