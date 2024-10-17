namespace ScrollSucker;

public class EnemyDirective : SpawnDirective
{
    public string Display { get => observable.Get<string>(); set => observable.Set(value); }
    [FormSlider(Increment = 10, Min = 1, Max = 1000)]
    public float HP { get => observable.Get<float>(); set => observable.Set(value); }
    public float Speed { get => observable.Get<float>(); set => observable.Set(value); }
    public bool StartWhenVisible { get => observable.Get<bool>(); set => observable.Set(value); }
    public EnemyDirective()
    {
        HP = 10;
        Speed = 10;
        StartWhenVisible = false;
    }

    public override GameCollider Preview(World w)
    {
        var enemy = Game.Current.GamePanel.Add(new Enemy(ConsoleString.Parse(Display)));
        enemy.AddTag("enemy");
        enemy.SetHP(HP, HP);
        w.Place(enemy, X, Top);
        return enemy;
    }

    private IEnumerable<GameCollider> Target() => Game.Current.GamePanel.Children.WhereAs<Player>();

    public override void Render(World w)
    {
        var enemy = Preview(w) as Enemy;

        var movement = () => Game.Current.Invoke(async () =>
        {
            Player player = null;
            // make sure the player is in the world
            while (player == null)
            {
                await Game.Current.Delay(100);
                player = Game.Current.GamePanel.Children.WhereAs<Player>().SingleOrDefault();
            }

            enemy.Velocity.Speed = Speed;
            enemy.Velocity.Angle = Angle.Left;
            while(enemy.CalculateNormalizedDistanceTo(player) > 50)
            {
                await Game.Current.Delay(100);
            }

            await Mover.InvokeWithShortCircuit(Charge.Create(enemy, () => Speed * 2, Target));
        });

        if (w.Camera.IsInView(enemy) || StartWhenVisible == false)
        {
            movement();
            return;
        }

        var oneTimeLt = enemy.CreateChildLifetime();
        w.Camera.Subscribe(nameof(w.Camera.CameraLocation), () =>
        {
            if (w.Camera.IsInView(enemy) == false) return;
            movement();
            oneTimeLt.Dispose();
        }, oneTimeLt);
    }

    public override void ValidateUserInput()
    {
        if (string.IsNullOrWhiteSpace(Display)) Display = "[Red]enemy";
        if (HP <= 0) HP = 10;
        if(Speed <= 0) Speed = 10;
    }

    public override void RemoveFrom(LevelSpec spec) => spec.Enemies.Remove(this);
    public override void AddTo(LevelSpec spec) => spec.Enemies.Add(this);
}

public class Enemy : StringCharacter
{
    public Enemy(ConsoleString display) : base(display)
    {
        AddTag(nameof(Enemy));
    }

    public override bool CanCollideWith(GameCollider other)
    {
        if (other is Enemy) return false;
        return base.CanCollideWith(other);
    }
}

public class Charge : CombatMovement
{
    public float CloseEnough { get; set; } = 5;

    private Charge(Character c, SpeedEval speed, TargetEval targetFunc) : base(c, speed, targetFunc) { }
    public static Movement Create(Character c, SpeedEval speed, TargetEval targetFunc) => new Charge(c, speed, targetFunc);
    protected override async Task Move()
    {
        while (this.IsExpired == false)
        {
            await Mover.InvokeOrTimeout(this, Wander.Create(Velocity, Speed, new WanderOptions()
            {
                CuriousityPoint = () => GetEffectiveTarget()
            }), new UntilCloseToTargetLifetime(Character, GetEffectiveTarget, CloseEnough));

            var targetEval = GetEffectiveTarget();
            await StayOnTarget(targetEval, CloseEnough);
            await Task.Yield();
        }
    }
}

public abstract class CombatMovement : Movement
{
    public delegate IEnumerable<GameCollider> TargetEval();
    public Character Character { get; protected set; }
    protected TargetEval TargetFunction { get; private set; }

    protected CombatMovement(Character c, SpeedEval speed, TargetEval targetFunc) : base(c.Velocity, speed)
    {
        this.Character = c;
        this.TargetFunction = targetFunc;
    }

    protected async Task<GameCollider> WaitForTargetAsync()
    {
        var target = GetEffectiveTarget();
        while (target == null && this.IsExpired == false)
        {
            await Mover.InvokeOrTimeout(this, Wander.Create(Velocity, Speed), Game.Current.Delay(200).ToLifetime());
            target = GetEffectiveTarget();
        }
        return target;
    }

    protected async Task StayOnTarget(GameCollider target, float closeEnough)
    {
        while (this.IsExpired == false && target != null && target.CalculateDistanceTo(Character) < closeEnough && Character.Velocity.HasLineOfSight(target))
        {
            Character.Velocity.Angle = Character.CalculateAngleTo(target);
            if (target.IsExpired) break;
            await AssertAlive();
            await YieldAsync();
        }
    }

  
    protected GameCollider GetEffectiveTarget() => GetEffectiveTargets().FirstOrDefault();
    protected IEnumerable<GameCollider> GetEffectiveTargets()
    {
        if (TargetFunction != null)
        {
            var ret = TargetFunction().Where(e => e != null).OrderBy(e => e.CalculateDistanceTo(Character)).ToList();
            return ret;
        }
        
        else
        {
            return new GameCollider[0];
        }

    }
}

public class UntilCloseToTargetLifetime : ILifetimeManager
{
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;
    public bool ShouldStop => !ShouldContinue;
    private Lifetime lt;
    public UntilCloseToTargetLifetime(Character c, Func<GameCollider> target, float closeEnough)
    {
        lt = Game.Current.CreateChildLifetime();
        Game.Current.Invoke(async () =>
        {
            var toggle = false;
            var targetEval = target();
            while (lt.IsExpired == false)
            {
                targetEval = toggle ? target() : targetEval;
                toggle = !toggle;

                if (targetEval != null && c.CalculateDistanceTo(targetEval) <= closeEnough && c.Velocity.HasLineOfSight(targetEval))
                {
                    lt.Dispose();
                    break;
                }

                await Task.Delay(250);
            }
        });
    }

    public bool IsExpired => lt.IsExpired;

    public bool IsExpiring => lt.IsExpiring;

    public void OnDisposed(Action cleanupCode) => lt.OnDisposed(cleanupCode);

    public void OnDisposed(IDisposable obj) => lt.OnDisposed(obj);
}