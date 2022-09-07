using klooie;
using klooie.Gaming;
using PowerArgs;
using PowerArgs.Cli;

public class Program
{
    public static void Main(string [] args)
    {
        Character c = null;
        var game = new TestGame(new GamingTestOptions()
        {
            CameraFocalPoint = ()=> c?.Center(),
            Camera = true,
        });
        game.Invoke(async () =>
        {
            AddTerrain(15,60,30);
            c = game.GamePanel.Add(new Character() { Background = RGB.Red });
            c.ResizeTo(3, 1);
            c.MoveTo(game.GameBounds.Left + 2, game.GameBounds.CenterY-9);
            await Task.Delay(1000);
            c.Velocity.Speed = 50;
        });
        
        game.Run();

    }

    private static void AddTerrain(float spacing, float w, float h)
    {
        var bounds = Game.Current.GameBounds;

        var leftWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        leftWall.MoveTo(bounds.Left, bounds.Top);
        leftWall.ResizeTo(2, bounds.Height);
        leftWall.GiveWiggleRoom();

        var rightWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        rightWall.MoveTo(bounds.Right - 2, bounds.Top);
        rightWall.ResizeTo(2, bounds.Height);
        rightWall.GiveWiggleRoom();

        var topWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        topWall.MoveTo(bounds.Left, bounds.Top);
        topWall.ResizeTo(bounds.Width, 1);
        topWall.GiveWiggleRoom();

        var bottonWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        bottonWall.MoveTo(bounds.Left, bounds.Bottom - 1);
        bottonWall.ResizeTo(Game.Current.GameBounds.Width, 1);
        bottonWall.GiveWiggleRoom();

        for (var x = bounds.Left + spacing; x < bounds.Right - spacing; x += w + spacing)
        {
            for (var y = bounds.Top + spacing / 2f; y < bounds.Bottom - spacing / 2; y += h + (spacing / 2f))
            {
                var collider = Game.Current.GamePanel.Add(new GameCollider());
                collider.ResizeTo(w, h);
                collider.MoveTo(x, y);
                collider.Background = RGB.DarkGreen;
            }
        }
    }
}

public class TestGame : Game
{
    protected override IRuleProvider RuleProvider => provider ?? ArrayRulesProvider.Empty;
    private IRuleProvider provider;
    private Camera camera;
    private GamingTestOptions options;
    public TestGame(GamingTestOptions options)
    {
        this.options = options;
    }

    protected override async Task Startup()
    {
        if (options == null)
        {
            await base.Startup();
            return;
        }
        this.provider = options.Rules;
        if (options.Camera)
        {
            this.camera = LayoutRoot.Add(new Camera()).Fill();
            camera.Background = new RGB(20, 20, 20);
            camera.BigBounds = new RectF(-500, -500, 1000, 1000);
            camera.PointAt(camera.BigBounds.Center);

            if (options.CameraFocalPoint != null)
            {
                Invoke(async () =>
                {
                    while (ShouldContinue)
                    {
                        await this.DelayOrYield(10);
                        var fp = options.CameraFocalPoint();
                        if (fp.HasValue)
                        {
                            camera.PointAt(fp.Value);
                        }
                    }
                });
            }
        }

        await base.Startup();
        return;
    }

    public TestGame() { }

    public override ConsolePanel GamePanel => camera ?? base.GamePanel;
    public override RectF GameBounds => camera?.BigBounds ?? base.GameBounds;
}

public class GamingTestOptions
{
    public IRuleProvider Rules { get; set; } = ArrayRulesProvider.Empty;
    public bool Camera { get; set; } = false;
    public Func<LocF?> CameraFocalPoint { get; set; } = null;
}