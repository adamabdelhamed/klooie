namespace ScrollSucker;

public class World : Game
{
    public const int SceneHeight = 12;
    public const int ViewportWidth = 125;

    protected Camera camera;
    protected LevelSpec spec;

    public override ConsolePanel GamePanel => camera;
    public override RectF GameBounds => camera.BigBounds;
    public LevelSpec Spec => spec;
    public Camera Camera => camera;
    public float TopDilemmaY => camera.BigBounds.Top + camera.BigBounds.Height* .33f;
    public float BottomDilemmaY => camera.BigBounds.Top + camera.BigBounds.Height * .66f;
    
    protected Random r = new Random();

    public World(LevelSpec spec)
    {
        LayoutRoot.Background = new RGB(20, 20, 20);
        this.spec = spec;
    }

    public bool IsTop(ConsoleControl c) =>  Math.Abs(TopDilemmaY - c.Top) < Math.Abs(BottomDilemmaY - c.Top);

    public void Place(GameCollider c, float x, bool top) => c.MoveTo(x, (top ? TopDilemmaY : BottomDilemmaY) - (c.Height / 2f));

    protected override async Task Startup()
    {
        await base.Startup();
        Sound = new ScrollSuckerSoundEngine();
        Sound.MasterVolume = .5f;
        AddCamera();
        AddTexture();
        new HP();
        KnockBackEffect.Initialize();
        Splatter.Initialize();
    }

    private void AddCamera()
    {
        camera = LayoutRoot.Add(new Camera() { Background = RGB.Black, BigBounds = new LocF().ToRect(spec.SceneWidth, SceneHeight) })
            .FillMax(maxWidth: ViewportWidth, maxHeight: SceneHeight);
        camera.Background = new RGB(20, 20, 30);
        camera.PointAt(new LocF(camera.BigBounds.Left, camera.BigBounds.CenterY));
    }

    private void AddTexture()
    {
        var bitmap = CityScape.Create((int)camera.BigBounds.Width, (int)camera.BigBounds.Height, new RGB(50,30,30), RGB.Black, new RGB(10,10,10), new RGB(20, 20, 20));
        var bitmapControl = camera.Add(new BitmapControl(bitmap) {   Width = bitmap.Width, Height = bitmap.Height });
        bitmapControl.MoveTo(camera.BigBounds.Left, camera.BigBounds.Top);
    }
}
