using klooie;
using klooie.Gaming;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace tests.Gaming.Movement;
[TestClass]
[TestCategory(Categories.Slow)]
public class WalkingTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Movement_WalkToPointsOfInterest() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext?.TestId() ?? nameof(Movement_WalkToPointsOfInterest),
        Mode = UITestMode.RealTimeFYI,
        GameWidth = 1000,
        GameHeight = 500,
        Camera = true,
        Test = async(context) =>
        {
            await Task.Yield();
            var game = Game.Current;
            game.LayoutRoot.Background = new RGB(15, 0, 0);

            var pointOfInterestCollider = game.GamePanel.Add(GameColliderPool.Instance.Rent());
            pointOfInterestCollider.MoveTo(WalkWithCustomPointsOfInterest.pointsOfInterest[0].Left, WalkWithCustomPointsOfInterest.pointsOfInterest[0].Top);
            pointOfInterestCollider.ResizeTo(WalkWithCustomPointsOfInterest.pointsOfInterest[0].Width, WalkWithCustomPointsOfInterest.pointsOfInterest[0].Height);
            pointOfInterestCollider.Background = RGB.Green;

            var scheduler = FrameTaskScheduler.Create(TimeSpan.FromSeconds(.25f), Game.Current.PauseManager);
            var walkerLocation = game.GameBounds.Center.Offset(-60, 0);
            var walker = game.GamePanel.Add(new GameCollider(walkerLocation.Left, walkerLocation.Top, 2, 1));

            walker.Background = RGB.Blue;
            var vision = Vision.Create(scheduler, walker);

            var visionFilter = new VisionFilter(vision);
            vision.AngleStep = 1;
            vision.MaxMemoryTime = TimeSpan.FromSeconds(0);
            var walkFunction = WalkWithCustomPointsOfInterest.Create(vision, 80);

            AddObstacles(game, visionFilter);

            Game.Current.AfterPaint.Subscribe(() =>
            {
                if (walker.CalculateNormalizedDistanceTo(WalkWithCustomPointsOfInterest.pointsOfInterest[walkFunction.CurrentPointOfInterestIndex]) < 2)
                {
                    walkFunction.CurrentPointOfInterestIndex++;
                    if (walkFunction.CurrentPointOfInterestIndex >= WalkWithCustomPointsOfInterest.pointsOfInterest.Length)
                    {
                        Game.Current.Stop();
                    }
                    pointOfInterestCollider.Dispose();
                    pointOfInterestCollider = game.GamePanel.Add(GameColliderPool.Instance.Rent());
                    pointOfInterestCollider.MoveTo(walkFunction.GetPointOfInterest().Value.Left, walkFunction.GetPointOfInterest().Value.Top);
                    pointOfInterestCollider.ResizeTo(walkFunction.GetPointOfInterest().Value.Width, walkFunction.GetPointOfInterest().Value.Height);
                    pointOfInterestCollider.Background = RGB.Green;
                    Console.WriteLine($"Reached point of interest {walkFunction.GetPointOfInterest().Value}");
                }

            }, Game.Current);
        }
    });

    private static void AddObstacles(Game game, VisionFilter visionFilter)
    {
        foreach (var angle in Angle.Enumerate360Angles(0, 25))
        {
            for (var distanceFromCenter = 25; distanceFromCenter < 60; distanceFromCenter += 20)
            {
                var obstacle = game.GamePanel.Add(GameColliderPool.Instance.Rent());

                obstacle.Filters.Add(visionFilter);
                var spot = game.GameBounds.Center.RadialOffset(angle, distanceFromCenter).GetRounded();
                obstacle.MoveTo(spot.Left, spot.Top);
                obstacle.ResizeTo(2, 1);
                obstacle.Background = RGB.Red;
            }
        }
    }
}
    
public class WalkWithCustomPointsOfInterest : Walk
{
    public static RectF[] pointsOfInterest =>
    [
        Game.Current.GameBounds.Center.Offset(35, 0).ToRect(4,2),
        Game.Current.GameBounds.Center.Offset(-35, 0).ToRect(4,2),
        Game.Current.GameBounds.Center.Offset(0, 17.5f).ToRect(4,2),
        Game.Current.GameBounds.Center.Offset(0, -17.5f).ToRect(4,2)
    ];

    public int CurrentPointOfInterestIndex { get; set; } = 0;
    protected WalkWithCustomPointsOfInterest() { }
    private static LazyPool<WalkWithCustomPointsOfInterest> pool = new LazyPool<WalkWithCustomPointsOfInterest>(() => new WalkWithCustomPointsOfInterest());
    public static WalkWithCustomPointsOfInterest Create(Vision vision, float speed = 1)
    {
        var state = pool.Value.Rent();
        state.Construct(vision, speed);
        state.CurrentPointOfInterestIndex = 0;
        return state;
    }

    public override RectF? GetPointOfInterest() => pointsOfInterest[CurrentPointOfInterestIndex];
}