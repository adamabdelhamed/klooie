





namespace klooie.Gaming;

public class ProjectileRule : IRule
{
    private static ColliderGroup shrapnelGroup;
    public Task ExecuteAsync()
    {
        Vision.VisionInitiated.Subscribe(OnVisionInitiated, Game.Current); // See below for this event
        Game.Current.MainColliderGroup.OnCollision.Subscribe(OnCollision, Game.Current);
        return Task.CompletedTask;
    }

    private static void OnCollision(Collision collision)
    {
        if (collision.MovingObject is Projectile == false || collision.ColliderHit is Projectile == false) return;

        shrapnelGroup = shrapnelGroup ?? new ColliderGroup(Game.Current, Game.Current.PauseManager);
        var a = (Projectile)collision.MovingObject;
        var b = (Projectile)collision.ColliderHit;

      
        if(a.Velocity == null || b.Velocity == null) return;

        var aAngle = ColliderGroup.ComputeBounceAngle(a.Velocity, b.Bounds, collision.Prediction);
        var bAngle = aAngle.Opposite();

        a.TryDispose("ProjectileRule.OnCollision - Auto Dispose w Shrapnel");
        b.TryDispose("ProjectileRule.OnCollision - Auto Dispose w Shrapnel");

        SpawnShrapnel(a, aAngle.Add(15+random.Next(-10,10)));
        SpawnShrapnel(a, aAngle.Add(-15 + random.Next(-10, 10)));
        SpawnShrapnel(b, bAngle.Add(15 + random.Next(-10, 10)));
        SpawnShrapnel(b, bAngle.Add(-15 + random.Next(-10, 10)));
    }

    public static void SimulateCollisionOnInitialPlacement(Projectile p, GameCollider obstacle)
    {
        var collision = CollisionPool.Instance.Rent();
        var angle = p.Velocity.Angle;
        var prediction = CollisionPredictionPool.Instance.Rent();
        prediction.ObstacleHitBounds = obstacle.Bounds;
        prediction.CollisionPredicted = true;
        prediction.LKGD = p.CalculateDistanceTo(obstacle);
        prediction.ColliderHit = obstacle;
        prediction.Edge = obstacle.Bounds.LeftEdge; // todo - compute properly
        collision.Bind(p.Velocity.Speed, angle, p, obstacle, prediction);
        p.ColliderGroup.OnCollision.Fire(collision);
        collision.Dispose();
        prediction.Dispose();
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
        Game.Current.PausableScheduler.Delay(random.Next(200,500), shrapnel, Recyclable.TryDisposeMe);
    }



    private static void OnVisionInitiated(Vision vision)
    {
        vision.TargetBeingEvaluated.Subscribe(OnVisionTargetBeingEvaluated, vision);
    }

    private static void OnVisionTargetBeingEvaluated(VisionFilterContext context)
    {
        if (context.PotentialTarget is Projectile)
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
    public void Bind(Weapon w, float speed, Angle angle, float? x = null, float? y = null)
    {
        if(w.Source == null) throw new InvalidOperationException("Weapon source is null");
        if(w.Source.Velocity == null) throw new InvalidOperationException("Weapon source velocity is null");
        base.Bind(w);
        CompositionMode = CompositionMode.BlendBackground;
        Velocity.Angle = angle;
        AddHolderSpeedToProjectileSpeedIfNeeded(speed, angle);
        if (TryPlace(w, angle, x, y))
        {
            Velocity.OnCollision.Subscribe(this, OnCollision, this);
            BoundsChanged.Subscribe(this, EnforceRangeStatic, this);
        }
    }
 
    private static void EnforceRangeStatic(object me)
    {
        var _this = (me as Projectile)!;
        if (_this.Range > 0 && _this.CalculateDistanceTo(_this.startLocation) > _this.Range)
        {
            _this.TryDispose(nameof(EnforceRangeStatic));
        }
    }

    private bool TryPlace(Weapon w, Angle angle, float? x = null, float? y = null)
    {
        this.ResizeTo(1, 1);
        x = x.HasValue ? x.Value : w.Source.CenterX() - (Width / 2f);
        y = y.HasValue ? y.Value : w.Source.CenterY() - (Height / 2f);
        this.MoveTo(x.Value, y.Value, w.Source.ZIndex);
        var offset = this.RadialOffset(angle, 1.5f, false);
        this.MoveTo(offset.Left, offset.Top);
        startLocation = this.Bounds;
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            this.GetObstacles(buffer);
            for (int i = 0; i < buffer.WriteableBuffer.Count; i++)
            {
                if(this.Touches(buffer.WriteableBuffer[i]))
                {
                    ProjectileRule.SimulateCollisionOnInitialPlacement(this, buffer.WriteableBuffer[i]);
                    this.TryDispose(nameof(TryPlace));
                    return false;
                }
            }
            return true;
        }
        finally
        {
            buffer.TryDispose();
        }
    }

    private void AddHolderSpeedToProjectileSpeedIfNeeded(float speed, Angle angle)
    {
        Velocity.speed = speed;
        if (Weapon.Source.Velocity.Angle.DiffShortest(angle) < 45)
        {
            Velocity.speed += Weapon.Source.Velocity.Speed;
        }
    }
    private static void OnCollision(Projectile me, Collision collision)
        => Game.Current.InvokeNextCycle(DisposeMe, ProjectileDelayedDisposalState.Create(me));


    private static void DisposeMe(object me)
    {
        var state = (ProjectileDelayedDisposalState)me;
        if(state.AreAllDependenciesValid == false)
        {
            state.Dispose();
            return;
        }

        state.Projectile.TryDispose("OnCollisionInvokeNextCycleDisposeMe");
        state.Dispose();
    }
    protected override void OnPaint(ConsoleBitmap context) => context.Fill(Pen);
}

public class ProjectileDelayedDisposalState : DelayState
{
    public Projectile Projectile { get; private set; }
    public static ProjectileDelayedDisposalState Create(Projectile p)
    {
        var ret = ProjectileDelayedDisposalStatePool.Instance.Rent();
        ret.AddDependency(p);
        ret.Projectile = p;
        return ret;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Projectile = null;
    }
}