using klooie;
using klooie.Gaming;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Reflection;
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

            int currentPointOfInterestIndex = 0;
            var pointsOfInterest = new[]
            {
                game.GameBounds.Center.Offset(35, 0).ToRect(4,2),
                game.GameBounds.Center.Offset(-35, 0).ToRect(4,2),
                game.GameBounds.Center.Offset(0, 17.5f).ToRect(4,2),
                game.GameBounds.Center.Offset(0, -17.5f).ToRect(4,2)
            };

            var pointOfInterestCollider = game.GamePanel.Add(GameColliderPool.Instance.Rent());
            pointOfInterestCollider.MoveTo(pointsOfInterest[0].Left, pointsOfInterest[0].Top);
            pointOfInterestCollider.ResizeTo(pointsOfInterest[0].Width, pointsOfInterest[0].Height);
            pointOfInterestCollider.Background = RGB.Green;

            var scheduler = FrameTaskScheduler.Create(TimeSpan.FromSeconds(.25f), Game.Current.PauseManager);
            var walkerLocation = game.GameBounds.Center.Offset(-60, 0);
            var walker = game.GamePanel.Add(new GameCollider(walkerLocation.Left, walkerLocation.Top, 2, 1));

            walker.Background = RGB.Blue;
            var vision = Vision.Create(scheduler, walker);

            var visionFilter = new VisionFilter(vision);
            vision.AngleStep = 1;
            vision.MaxMemoryTime = TimeSpan.FromSeconds(0);
            var walkFunction = Walk.Create(vision, (wander) => pointsOfInterest[currentPointOfInterestIndex], 80);

            AddObstacles(game, visionFilter);

            Game.Current.AfterPaint.Subscribe(() =>
            {
                if (walker.CalculateNormalizedDistanceTo(pointsOfInterest[currentPointOfInterestIndex]) < 2)
                {
                    currentPointOfInterestIndex++;
                    if (currentPointOfInterestIndex >= pointsOfInterest.Length)
                    {
                        Game.Current.Stop();
                    }
                    pointOfInterestCollider.Dispose();
                    pointOfInterestCollider = game.GamePanel.Add(GameColliderPool.Instance.Rent());
                    pointOfInterestCollider.MoveTo(pointsOfInterest[currentPointOfInterestIndex].Left, pointsOfInterest[currentPointOfInterestIndex].Top);
                    pointOfInterestCollider.ResizeTo(pointsOfInterest[currentPointOfInterestIndex].Width, pointsOfInterest[currentPointOfInterestIndex].Height);
                    pointOfInterestCollider.Background = RGB.Green;
                    Console.WriteLine($"Reached point of interest {pointsOfInterest[currentPointOfInterestIndex - 1]}");
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
    
