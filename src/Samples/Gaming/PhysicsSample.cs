//#Sample -Id PhysicsSample
using PowerArgs;
using klooie;
using klooie.Gaming;
namespace klooie.Samples;

// Define your application
public class PhysicsSample : Game
{
    private static readonly ConsoleString[] Strings = new ConsoleString[]
    {
        "hello world".ToMagenta(),
        "klooie".ToGreen(),
        "PowerArgs".ToOrange(),
        "fun with physics".ToYellow(),

    };
    private Random random = new Random();
    protected override async Task Startup()
    {
        await base.Startup();

        AddWalls();
        for (var i = 0; i < 10; i++)
        {
            AddRandomWhiteSquare();
        }

        for (var i = 0; i < 10; i++)
        {
            AddRandomTextCollider();
        }

        // schedule a background task that waits 5 seconds, pauses the game for 2 seconds, then resumes
        Invoke(async () =>
        {
            await Task.Delay(5000);
            Pause();// non-blocking
            var pauseMessage = "The game is paused".ToYellow();
            var pauseLt = Task.Delay(2000).ToLifetime();
            await MessageDialog.Show(new ShowMessageOptions(pauseMessage) { MaxLifetime = pauseLt });
            Resume();
        });

        await Task.Delay(10000);
        Stop();
    }

    private void AddWalls()
    {
        var leftWall = GamePanel.Add(new GameCollider() { Background = RGB.Orange, Bounds = new RectF(GamePanel.Left,GamePanel.Top ,2,Height) });
        var rightWall = GamePanel.Add(new GameCollider() { Background = RGB.Orange, Bounds = new RectF(GamePanel.Right()-2, GamePanel.Top, 2, Height) });
        var topWall = GamePanel.Add(new GameCollider() { Background = RGB.Orange, Bounds = new RectF(GamePanel.Left, GamePanel.Top, Width, 1) });
        var bottomWall = GamePanel.Add(new GameCollider() { Background = RGB.Orange, Bounds = new RectF(GamePanel.Left, GamePanel.Bottom()-1, Width, 1) });
    }

    private void AddRandomWhiteSquare()
    {
        var randomLocation = FindRandomEmptyLocation(2, 1);
        if (randomLocation.HasValue)
        {
            var whiteSquare = GamePanel.Add(new GameCollider()
            {
                Background = RGB.White,
                Bounds = new RectF(randomLocation.Value.Left, randomLocation.Value.Top, 2, 1)
            });
            whiteSquare.Velocity.Speed = random.Next(1, 100);
            whiteSquare.Velocity.Angle = random.Next(0, 360);
            whiteSquare.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
            WhiteSquaresTurnRedAndFadeBackToWhiteOnCollision(whiteSquare);
        }
    }

    private void AddRandomTextCollider()
    {
        var randomString = Strings[random.Next(0, Strings.Length)];
        var randomLocation = FindRandomEmptyLocation(randomString.Length, 1);
        if (randomLocation.HasValue)
        {
            var textCollider = GamePanel.Add(new TextCollider(randomString)
            {
                Bounds = new RectF(randomLocation.Value.Left, randomLocation.Value.Top, randomString.Length, 1)
            });
            textCollider.Velocity.Speed = random.Next(1, 100);
            textCollider.Velocity.Angle = random.Next(0, 360);
            textCollider.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
        }
    }

    private static void WhiteSquaresTurnRedAndFadeBackToWhiteOnCollision(GameCollider whiteSquare)
    {
        whiteSquare.Velocity.OnCollision.Subscribe(async (collisionInfo) =>
        {
            if (whiteSquare.Background != RGB.White) return;
            await Animator.AnimateAsync(Animator.RGBAnimationState.Create(new List<KeyValuePair<RGB, RGB>>() { new KeyValuePair<RGB, RGB>(RGB.Red, RGB.White) }, colors => whiteSquare.Background = colors[0], 1000));
        }, whiteSquare);
    }

    private LocF? FindRandomEmptyLocation(float w, float h)
    {
        var obstacles = GamePanel.Controls.ToList();
        var randomLocInArea = new LocF(random.Next((int)GameBounds.Left + 1, (int)GameBounds.Right - ((int)w + 1)),
            random.Next((int)GameBounds.Top + 1, (int)GameBounds.Bottom - ((int)h + 1)));
        foreach (var startingPoint in new LocF[] { randomLocInArea, GameBounds.Center })
        {
            foreach (var spacing in new float[] { 8, 3, 1 })
            {
                foreach (var angle in Angle.Enumerate360Angles(0))
                {
                    for (var d = 1f; d < GameBounds.Hypotenous; d += spacing)
                    {
                        var testLoc = startingPoint.RadialOffset(angle, d).Offset(-w / 2f, -h / 2f);
                        var testArea = new RectF(testLoc.Left, testLoc.Top, w, h);
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

// Entry point for your application
public static class PhysicsSampleProgram
{
    public static void Main() => new PhysicsSample().Run();
}
//#EndSample


public class PhysicsSampleRunner : IRecordableSample
{
    public string OutputPath => @"Gaming\PhysicsSample.gif";

    public int Width => 120;

    public int Height => 40;

    public ConsoleApp Define()
    {
        var ret = new PhysicsSample();
        return ret;
    }
}