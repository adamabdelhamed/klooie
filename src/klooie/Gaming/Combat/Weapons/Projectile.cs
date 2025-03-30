





namespace klooie.Gaming;

public class ProjectileRule : IRule
{
    private static ColliderGroup shrapnelGroup;
    public Task ExecuteAsync()
    {
        Targeting.TargetingInitiated.Subscribe(OnTargetingInitiated, Game.Current);
        Game.Current.MainColliderGroup.OnCollision.Subscribe(OnCollision, Game.Current);
        return Task.CompletedTask;
    }

    private void OnCollision(Collision collision)
    {
        if (collision.MovingObject is Projectile == false || collision.ColliderHit is Projectile == false) return;

        shrapnelGroup = shrapnelGroup ?? new ColliderGroup(Game.Current);
        var a = (Projectile)collision.MovingObject;
        var b = (Projectile)collision.ColliderHit;

      
        if(a.Velocity == null || b.Velocity == null) return;

        var aAngle = ColliderGroup.ComputeBounceAngle(a, b.Bounds, collision.Prediction);
        var bAngle = aAngle.Opposite();

        SpawnShrapnel(a, aAngle.Add(15+random.Next(-10,10)));
        SpawnShrapnel(a, aAngle.Add(-15 + random.Next(-10, 10)));
        SpawnShrapnel(b, bAngle.Add(15 + random.Next(-10, 10)));
        SpawnShrapnel(b, bAngle.Add(-15 + random.Next(-10, 10)));
    }

    private static ConsoleString pen = ".".ToYellow();
    private static Random random = new Random();
    private static void SpawnShrapnel(Projectile p, Angle angle)
    {
        var shrapnel = ShrapnelPool.Instance.Rent();
        shrapnel.CompositionMode = CompositionMode.BlendBackground;
        shrapnel.ConnectToGroup(shrapnelGroup);
        shrapnel.MoveTo(p.TopLeft());
        shrapnel.Content = pen;
        shrapnel.Velocity.Speed = 50;
        shrapnel.Velocity.Angle = angle;
        Game.Current.GamePanel.Add(shrapnel);
        Game.Current.InnerLoopAPIs.Delay(random.Next(200,500), shrapnel, DisposeShrapnel);
    }

    private static void DisposeShrapnel(object obj) => ((Recyclable)obj).TryDispose();
    

    private void OnTargetingInitiated(Targeting targeting)
    {
        targeting.TargetBeingEvaluated.Subscribe(OnTargetBeingEvaluated, targeting);
    }

    private void OnTargetBeingEvaluated(TargetFilterContext context)
    {
        if (context.PotentialTarget is Projectile == true)
        {
            context.IgnoreTargeting();
        }
    }
}

public partial class Shrapnel : TextCollider
{
    public override bool CanCollideWith(ICollidable other) => false;
}

public class Projectile : WeaponElement
{
    private static readonly ConsoleCharacter DefaultPen = new ConsoleCharacter('*', RGB.Red);
    public ConsoleCharacter Pen { get; set; } = DefaultPen;
    public float Range { get; set; } = 150;
    private RectF startLocation;
    public void Bind(Weapon w, float speed, Angle angle)
    {
        if(w.Source == null) throw new InvalidOperationException("Weapon source is null");
        if(w.Source.Velocity == null) throw new InvalidOperationException("Weapon source velocity is null");
        base.Bind(w);
        CompositionMode = CompositionMode.BlendBackground;
        Velocity.Angle = angle;
        AddHolderSpeedToProjectileSpeedIfNeeded(speed, angle);
        Place(w, angle);
        Velocity.OnCollision.Subscribe(this, OnCollision, this);
        BoundsChanged.Subscribe(this, EnforceRangeStatic, this);
    }
 
    private static void EnforceRangeStatic(object me)
    {
        var _this = (me as Projectile)!;
        if (_this.Range > 0 && _this.CalculateDistanceTo(_this.startLocation) > _this.Range)
        {
            _this.TryDispose();
        }
    }

    private void Place(Weapon w, Angle angle)
    {
        this.ResizeTo(1, 1);
        this.MoveTo(w.Source.CenterX() - (Width / 2f), w.Source.CenterY() - (Height / 2f), w.Source.ZIndex);
        var offset = this.RadialOffset(angle, 1, false);
        this.MoveTo(offset.Left, offset.Top);
        startLocation = this.Bounds;
    }

    private void AddHolderSpeedToProjectileSpeedIfNeeded(float speed, Angle angle)
    {
        Velocity.speed = speed;
        if (Weapon.Source.Velocity.Angle.DiffShortest(angle) < 45)
        {
            Velocity.speed += Weapon.Source.Velocity.Speed;
        }
    }

    private static void OnCollision(object me, object collision) =>  (me as Projectile)!.TryDispose();
    protected override void OnPaint(ConsoleBitmap context) => context.Fill(Pen);
}