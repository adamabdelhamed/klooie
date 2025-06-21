using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using klooie;
using klooie.Gaming;

[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 1, iterationCount: 5)]
public class IntersectionBenchmarks
{
    private float x, y;

    // Test cases
    private Edge intersectA = new Edge(0, 0, 10, 0);
    private Edge intersectB = new Edge(5, -5, 5, 5);

    private Edge parallelA = new Edge(0, 0, 10, 0);
    private Edge parallelB = new Edge(0, 1, 10, 1);

    private Edge collinearNoOverlapA = new Edge(0, 0, 10, 0);
    private Edge collinearNoOverlapB = new Edge(20, 0, 30, 0);

    private Edge collinearOverlapA = new Edge(0, 0, 10, 0);
    private Edge collinearOverlapB = new Edge(5, 0, 15, 0);

    private Edge endpointTouchA = new Edge(0, 0, 10, 0);
    private Edge endpointTouchB = new Edge(10, 0, 20, 0);

    private Edge verticalA = new Edge(5, 0, 5, 10);
    private Edge verticalB = new Edge(0, 5, 10, 5);

    private Edge outsideIntersectionA = new Edge(0, 0, 2, 0);
    private Edge outsideIntersectionB = new Edge(3, -1, 3, 1);

    private Edge identicalA = new Edge(0, 0, 10, 10);
    private Edge identicalB = new Edge(0, 0, 10, 10);

    // Use Params to select test case in the benchmark run
    [Params(
        nameof(Intersect),
        nameof(Parallel),
        nameof(CollinearNoOverlap),
        nameof(CollinearOverlap),
        nameof(EndpointTouch),
        nameof(VerticalHorizontalCross),
        nameof(OutsideIntersection),
        nameof(Identical)
    )]
    public string CaseName;

    private Edge ray, stationary;

    [GlobalSetup]
    public void Setup()
    {
        // Map CaseName to the correct pair
        switch (CaseName)
        {
            case nameof(Intersect):
                ray = intersectA; stationary = intersectB; break;
            case nameof(Parallel):
                ray = parallelA; stationary = parallelB; break;
            case nameof(CollinearNoOverlap):
                ray = collinearNoOverlapA; stationary = collinearNoOverlapB; break;
            case nameof(CollinearOverlap):
                ray = collinearOverlapA; stationary = collinearOverlapB; break;
            case nameof(EndpointTouch):
                ray = endpointTouchA; stationary = endpointTouchB; break;
            case nameof(VerticalHorizontalCross):
                ray = verticalA; stationary = verticalB; break;
            case nameof(OutsideIntersection):
                ray = outsideIntersectionA; stationary = outsideIntersectionB; break;
            case nameof(Identical):
                ray = identicalA; stationary = identicalB; break;
        }
    }

    // Old implementation as baseline
    [Benchmark(Baseline = true)]
    public bool Old() => CollisionDetector.TryFindIntersectionPointOld(ray, stationary, out x, out y);

    // New implementation
    [Benchmark]
    public bool New() => CollisionDetector.TryFindIntersectionPoint(ray, stationary, out x, out y);


    // --- Case names for Params attribute ---
    public static string Intersect => "Intersect";
    public static string Parallel => "Parallel";
    public static string CollinearNoOverlap => "CollinearNoOverlap";
    public static string CollinearOverlap => "CollinearOverlap";
    public static string EndpointTouch => "EndpointTouch";
    public static string VerticalHorizontalCross => "VerticalHorizontalCross";
    public static string OutsideIntersection => "OutsideIntersection";
    public static string Identical => "Identical";
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<IntersectionBenchmarks>();
    }
}
