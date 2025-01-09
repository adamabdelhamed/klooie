using BenchmarkDotNet.Attributes;
using klooie;
using klooie.Gaming;
using NAudio.Wave;
using Playground;
using PowerArgs;

public class Program
{
    static int count = 0;
    public static async Task Main(string[] args)
    {
 
       var game = new Game();
        game.Invoke(async () =>
        {
            await WanderTest(20, 500000, false, null, false);
        });
        game.Run();
    }

    public static async Task WanderTest(float speed, float duration, bool camera, Func<GameCollider> factory, bool extraTight)
    {
        Console.WriteLine("Speed: " + speed);

        if (extraTight)
        {
            AddTerrain(5f, 2, 1);
        }
        else if (camera)
        {
            AddTerrain(15, 60, 30);
        }
        else
        {
            AddTerrain(15, 6, 3);
        }
        Game.Current.LayoutRoot.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(factory != null ? factory() : new GameCollider());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(Game.Current.GameBounds.Left + 4, Game.Current.GameBounds.Top + 3);
        cMover.Velocity.OnSpeedChanged.Subscribe(() =>
        {
            if (cMover.Velocity.Speed == 0)
            {

            }
            else
            {

            }
        }, cMover);

        cMover.OnDisposed(() =>
        {

        });
        cMover.BoundsChanged.Subscribe(() =>
        {
            var buffer = ObstacleBufferPool.Instance.Rent();
            cMover.GetObstacles(buffer);
            var overlaps = buffer.ReadableBuffer
            .Where(o => o.OverlapPercentage(cMover) > 0).ToArray();
            ObstacleBufferPool.Instance.Return(buffer);
            if (overlaps.Any())
            {
                throw new Exception("overlaps detected");
            }
        }, cMover);
        if(cMover.NudgeFree(maxSearch: 50)  == false) throw new Exception("Failed to nudge free");
        cMover.Velocity.Angle = 45;
        var failed = false;
        var lastPosition = cMover.Center();
        Game.Current.Invoke(async () =>
        {
            var dueTime = Game.Current.MainColliderGroup.Now + TimeSpan.FromMilliseconds(duration);
            while (Game.Current.MainColliderGroup.Now < dueTime)
            {
                await Game.Current.Delay(1000);
                var newPosition = cMover.Center();
                var d = lastPosition.CalculateNormalizedDistanceTo(newPosition);

                failed = failed || d == 0; // we didn't get stuck
                if (extraTight) failed = false;
               // Console.WriteLine(failed ? d + " fail" : d + " ok");
                lastPosition = newPosition;
            }
            cMover.Dispose();
        });
        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
        await Mover.InvokeWithShortCircuit(Wander.Create(cMover.Velocity, () => speed, new WanderOptions()
        {
            CuriousityPoint = () =>
            {
                return extraTight ? new ColliderBox(Game.Current.GameBounds.Center.ToRect(1, 1)) : null;
            },
        }));
        if (cMover.IsExpired == false) throw new Exception("Failed to expire");
        if(failed) throw new Exception("Failed to keep moving");
       
        if (extraTight)
        {
            if (lastPosition.CalculateDistanceTo(Game.Current.GameBounds.Center) < 5) throw new Exception("Too far from center");
        }

        Game.Current.GamePanel.Controls.Clear();
        Console.WriteLine();
    }

    public static void AddTerrain(float spacing, float w, float h)
    {
        var bounds = Game.Current.GameBounds;

        var leftWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        leftWall.MoveTo(bounds.Left, bounds.Top);
        leftWall.ResizeTo(2, bounds.Height);
        leftWall.GiveWiggleRoom();

        var rightWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        rightWall.MoveTo(bounds.Right - 2, bounds.Top);
        rightWall.ResizeTo(2, bounds.Height);
        rightWall.GiveWiggleRoom();

        var topWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        topWall.MoveTo(bounds.Left, bounds.Top);
        topWall.ResizeTo(bounds.Width, 1);
        topWall.GiveWiggleRoom();

        var bottonWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        bottonWall.MoveTo(bounds.Left, bounds.Bottom - 1);
        bottonWall.ResizeTo(Game.Current.GameBounds.Width, 1);
        bottonWall.GiveWiggleRoom();

        var outerSpacing = Math.Max(5, spacing);
        for (var x = bounds.Left + outerSpacing; x < bounds.Right - outerSpacing; x += w + spacing)
        {
            for (var y = bounds.Top + outerSpacing / 2f; y < bounds.Bottom - outerSpacing / 2; y += h + (spacing / 2f))
            {
                if (new RectF(x, y, w, h).Center.CalculateDistanceTo(Game.Current.GameBounds.Center) > 5)
                {
                    var collider = Game.Current.GamePanel.Add(new Terrain());
                    collider.ResizeTo(w, h);
                    collider.MoveTo(x, y);
                    collider.Background = RGB.DarkGreen;
                }
            }
        }
    }
    public class Terrain : GameCollider { }
    public class OuterWall : GameCollider { }

    private static void RentSubscribeDisposeAndReturnOneMillionEvents()
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            var ev = EventPool.Rent();
            var r = DefaultRecyclablePool.Instance.Rent();
            ev.Subscribe(StaticDispose, r);
            ev.Fire();
            DefaultRecyclablePool.Instance.Return(r);
            EventPool.Return(ev);
        }
    }

  

    private static void StaticDispose()
    {
        count++;
    }
}

public class TestEngine : AudioPlaybackEngine
{
    protected override Dictionary<string, Func<Stream>> LoadSounds() => ResourceFileSoundLoader.LoadSounds<Sounds>();
}

public class SpinState
{
    public EventLoop Loop { get; set; }
    public long Start { get; set; }
    public TimeSpan SpinTime { get; set; }
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