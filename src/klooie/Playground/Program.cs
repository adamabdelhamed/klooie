using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using klooie;
using klooie.Gaming;
using klooie.tests;



public class Program
{
    public static void Main(string [] args)
    {
        new NavigationAtScaleGame().Run();
        //BenchmarkRunner.Run<Benchmarks>();
    }
}

[MemoryDiagnoser]
public class Benchmarks
{
    private List<RectF> obstacles;
    public Benchmarks()
    {
        var rand = new Random(454);
        obstacles = Enumerable.Range(0, 500)
            .Select(i => new RectF(rand.Next(100, 900), rand.Next(100, 900), 1, 1))
            .ToList();
        AStar.FindPath(1000, 1000, new RectF(0, 0, 1, 1), new RectF(900, 900, 1, 1), obstacles);
    }

    [Benchmark]
    public void BenchAStar()
    {
        AStar.FindPath(1000, 1000, new RectF(0, 0, 1, 1), new RectF(900, 900, 1, 1), obstacles);
    }
}