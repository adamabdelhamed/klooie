using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CLIborg;
using klooie;
using klooie.Gaming;
using NAudio.Wave;
using Playground;
using PowerArgs;

public class Program
{
    static int count = 0;
    public static void Main(string[] args)
    {
        new PhysicsSample().Run();
        // GameEx();
    }

    private static ColliderGroup splatterGroup;
    public static void GameEx()
    {
        var poolManager = PoolManager.Instance;
        poolManager.Get<SubscriptionPool>().Fill(10_000);
        poolManager.Get<DefaultRecyclablePool>().Fill(10_000);
        poolManager.Get<ObservableCollectionPool<IConsoleControlFilter>>().Fill(100);
        poolManager.Get<ObservableCollectionPool<ConsoleControl>>().Fill(100);

        var game = new Game();
        game.Invoke(async () =>
        {
            splatterGroup = new ColliderGroup(game, null);
            EnableShellEjection(game);

            var player = game.GamePanel.Add(new GameCollider() { Background = RGB.Green });
            player.MoveTo(game.GameBounds.Center.GetRounded());

            var vision = Vision.Create(player);

            var targeting = TargetingPool.Instance.Rent();
            targeting.Bind(new TargetingOptions() { Vision = vision, Source = player, HighlightTargets = true  });

            var pistol = PistolPool.Instance.Rent();
            pistol.AmmoAmount = 1000;
            pistol.Bind(targeting);
           

            var trigger = TriggerPool.Instance.Rent();
            trigger.Bind(pistol, ConsoleKey.Spacebar.KeyInfo(), ConsoleKey.Spacebar.KeyInfo(shift: true));


            var movement = TopDownHumanMovementInputPool.Instance.Rent();
            movement.Bind(player);
            var enemy = game.GamePanel.Add(new GameCollider() { Background = RGB.Red, Bounds = new RectF(0,0,.8f,.8f) });
            enemy.MoveTo(game.GameBounds.Center.GetRounded().Offset(20.1f, .1f));
        });

        game.Run();
    }

    public static void EnableShellEjection(ILifetime? lt = null)
    {
        lt = lt ?? Game.Current ?? throw new Exception();
        Weapon.OnFire.Subscribe(TryEjectShell, lt);
    }

    private static void TryEjectShell(Weapon w)
    {
        if (w is Pistol p == false) return;
        Splatter.TryEjectShell(p.Source.Bounds.Center, p.LastFireAngle.Opposite(), splatterGroup);
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
        var cMoverLease = cMover.Lease;
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

        
        cMover.BoundsChanged.Subscribe(() =>
        {
            var buffer = ObstacleBufferPool.Instance.Rent();
            cMover.GetObstacles(buffer);
            var overlaps = buffer.ReadableBuffer
            .Where(o => o.Bounds.OverlapPercentage(cMover.Bounds) > 0).ToArray();
            buffer.Dispose();
            if (overlaps.Any())
            {
                throw new Exception("overlaps detected");
            }
        }, cMover);

        var vision = Vision.Create(cMover);

        if (cMover.NudgeFree(maxSearch: 50)  == false) throw new Exception("Failed to nudge free");
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
        await Mover.Invoke(Wander.Create(new WanderOptions()
        {
            Speed = ()=>speed,
            Vision = vision,
            Velocity = cMover.Velocity,
            CuriousityPoint = () => extraTight ? Game.Current.GameBounds.Center.ToRect(1, 1) : null,
        }));
        if (cMover.IsStillValid(cMoverLease)) throw new Exception("Failed to expire");
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
    public sealed class Terrain : GameCollider { }
    public sealed class OuterWall : GameCollider { }

    private static void RentSubscribeDisposeAndReturnOneMillionEvents()
    {
        for (var i = 0; i < 1_000_000; i++)
        {
            var ev = Event.Create();
            var r = DefaultRecyclablePool.Instance.Rent();
            ev.Subscribe(StaticDispose, r);
            ev.Fire();
            r.Dispose();
            ev.Dispose();
        }
    }

  

    private static void StaticDispose()
    {
        count++;
    }
}


public sealed class Wall : GameCollider { }
public sealed class  Ball : GameCollider
{
    
}
public class PhysicsSample : Game
{
    private Camera camera;
    public override ConsolePanel GamePanel => camera;
    public override RectF GameBounds => camera.BigBounds;

    private Random random = new Random();
    protected override async Task Startup()
    {
        await base.Startup();
        LayoutRoot.Background = RGB.Green;
        var bigBounds = new Loc().ToRect(800, 60);
        camera = LayoutRoot.Add(new Camera() {  BigBounds = bigBounds }).FillMax(maxWidth: (int)bigBounds.Width, maxHeight: (int)bigBounds.Height);
        camera.EnableKeyboardPanning();
        camera.Background = RGB.Blue;
        LayoutRoot.BoundsChanged.Sync(() =>
        {
            camera.PointAt(GameBounds.Center);
        }, camera);
        AddWalls();
        var ballCount = 0;
        for(var x = bigBounds.Left+2; x < bigBounds.Right-3; x+=5)
        {
            for(var y = bigBounds.Top+1; y < bigBounds.Bottom-2; y+=3)
            {
                AddWhiteSquare(x, y);
                ballCount++;
            }
        }
        
        camera.Background = RGB.Black;
        Invoke(async () =>
        {
            var c = GamePanel.Controls.WhereAs<GameCollider>().Skip(10).First();
            c.Filters.Add(new BackgroundColorFilter(RGB.Red));
            while (true)
            {
                await Delay(100);
                c.Velocity.SpeedRatio = Math.Min(3, c.Velocity.SpeedRatio + .1f);
            }
        });
    }

    private void AddWalls()
    {
        var center = GamePanel.Add(new ConsoleStringRenderer("C".ToRed()) { ZIndex = int.MaxValue });
        var leftWall = GamePanel.Add(new Wall() { ZIndex = int.MinValue, Background = RGB.Orange, Bounds = new RectF(GameBounds.Left, GameBounds.Top, 2, GameBounds.Height) });
        var rightWall = GamePanel.Add(new Wall() { ZIndex = int.MinValue, Background = RGB.Orange, Bounds = new RectF(GameBounds.Right - 2, GameBounds.Top, 2, GameBounds.Height) });
        var topWall = GamePanel.Add(new Wall() { ZIndex = int.MinValue, Background = RGB.Orange, Bounds = new RectF(GameBounds.Left, GameBounds.Top, GameBounds.Width, 1) });
        var bottomWall = GamePanel.Add(new Wall() { ZIndex = int.MinValue, Background = RGB.Orange, Bounds = new RectF(GameBounds.Left, GameBounds.Bottom - 1, GameBounds.Width, 1) });
    }

    private void AddRandomWhiteSquare()
    {
        var randomLocation = FindRandomEmptyLocation(2, 1);
        if (randomLocation.HasValue)
        {
            var whiteSquare = GamePanel.Add(new Ball()
            {
                Background = RGB.White,
                Bounds = new RectF(randomLocation.Value.Left, randomLocation.Value.Top, 2, 1)
            });
            whiteSquare.Velocity.Speed = 30f;
            whiteSquare.Velocity.Angle = random.Next(0, 360);
            whiteSquare.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
        }
    }

    private void AddWhiteSquare(float x, float y)
    {
       
            var whiteSquare = GamePanel.Add(new Ball()
            {
                Background = RGB.White,
                Bounds = new RectF(x, y, 2, 1)
            });
            whiteSquare.Velocity.Speed = 30f;
            whiteSquare.Velocity.Angle = random.Next(0, 360);
            whiteSquare.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
        
    }


    private LocF? FindRandomEmptyLocation(float w, float h)
    {
        var obstacles = GamePanel.Controls.ToList();
        var randomLocInArea = new LocF(random.Next((int)GameBounds.Left + 1, (int)GameBounds.Right - ((int)w + 1)),
            random.Next((int)GameBounds.Top + 1, (int)GameBounds.Bottom - ((int)h + 1)));
        foreach (var startingPoint in new LocF[] { randomLocInArea, GameBounds.Center })
        {
            foreach (var spacing in new float[] { 8, 3, 1, .5f })
            {
                foreach (var angle in Angle.Enumerate360Angles(0))
                {
                    for (var d = 1f; d < GameBounds.Hypotenous; d += spacing)
                    {
                        var testLoc = startingPoint.RadialOffset(angle, d).Offset(-w / 2f, -h / 2f);
                        var testArea = new RectF(testLoc.Left, testLoc.Top, w, h);
                        if (Game.Current.GameBounds.Contains(testArea) == false) continue;
                        if (obstacles.Where(o => o.Bounds.CalculateNormalizedDistanceTo(testArea) < spacing).None())
                        {
                            return testArea.TopLeft;
                        }
                    }
                }
            }
        }
        return null;
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

public class MotionTracker
{
    private RectF lastKnownBounds;
    private int sameBoundsCount;
    private Velocity v;
    public MotionTracker(Velocity v)
    {
        this.v = v;
        var vLease = v.Lease;
        lastKnownBounds = v.Collider.Bounds;
        Game.Current.Invoke(async () =>
        {
            while(v.IsStillValid(vLease))
            {
                Track();
                await Game.Current.Delay(100);
            }
        });
    }

    private void Track()
    {
        sameBoundsCount = v.Collider.Bounds.Equals(lastKnownBounds) ? sameBoundsCount + 1 : 0;
        lastKnownBounds = v.Collider.Bounds;

       
        (v.Collider as GameCollider).Background = sameBoundsCount > 1 ? RGB.Red : RGB.White;
        (v.Collider as GameCollider).Tag = sameBoundsCount > 1 ? "Stuck" : null;
    }
}