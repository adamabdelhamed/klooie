
namespace ScrollSucker;

public class SmokeElement : GameCollider
{

    public ConsoleCharacter Display { get; set; } = new ConsoleCharacter('o', RGB.DarkGray);

    public int MaxAngleDiff { get; set; } = 20;
    public int MaxSpeedDiff { get; set; } = 2;

    public SmokeElement(TimeSpan? timeToLive = null, int maxInitialSpeed = 10)
    {
        CompositionMode = CompositionMode.BlendBackground;
        var ttl = timeToLive.HasValue ? timeToLive.Value : TimeSpan.FromSeconds(5);
        ResizeTo(3, 1.5f);

        var r = new Random();
        Velocity.Angle = r.Next(0, 360);
        Velocity.Speed = r.Next(0, maxInitialSpeed);

        Game.Current.Invoke(async () =>
        {
            var endTime = Game.Current.MainColliderGroup.Now.Add(ttl);
            while (Game.Current.MainColliderGroup.Now < endTime)
            {
                if (BeforeDelay() == false) break;

                await Game.Current.Delay(300);
                Velocity.Angle = Velocity.Angle.Add(r.Next(-MaxAngleDiff, MaxAngleDiff));
                Velocity.Speed += r.Next(-MaxSpeedDiff, MaxSpeedDiff);
            }
            this.Dispose();
        });
    }

    public override bool CanCollideWith(GameCollider other) => false;
    protected virtual bool BeforeDelay() => true;

    protected override void OnPaint(ConsoleBitmap context) => context.Fill(Display);
}

 
public class SmokeFunction : Lifetime
{
    private class MemoRecord
    {
        public int MemoX { get; set; }
        public int MemoY { get; set; }
        public RectF SceneBounds { get; set; }
        public float DistanceFromCenter { get; set; }
        public bool IsBlocked { get; set; }
        public bool LOS { get; set; }
    }

    public float EffectiveRange { get; private set; }
    public LocF ExplodeLocation { get; private set; }
    public int Range { get; private set; }

    private Character holder;
    private MemoRecord[][] memo;
    Dictionary<LocF, MemoRecord> reverseMemo;
    private bool alt;

    private static List<SmokeFunction> smokeFunctions = new List<SmokeFunction>();

    public SmokeFunction(Character holder, int range, bool alt)
    {
        this.holder = holder;
        this.Range = range;
        this.alt = alt;
        smokeFunctions.Add(this);
        this.OnDisposed(() =>
        {
            smokeFunctions.Remove(this);
        });
        Game.Current.Invoke(DoSmokeAsync);
    }

    public static bool IsConcealed(Character c, CollisionPrediction prediction, List<Edge> edgesHit) => smokeFunctions
        .Where(smoke => smoke.IsConcealedInternal(c, prediction, edgesHit))
        .Any();

    private bool IsConcealedInternal(Character c, CollisionPrediction prediction, List<Edge> edgesHit)
    {
        if (ExplodeLocation == null || c != holder || c == null || reverseMemo == null) return false;

        // If all the rays that targeted this character intersect this smoke's circle then the character
        // is considered concealed.
        var concealedRayCount = 0;
        foreach (var ray in edgesHit)
        {
            var intersectionCount = Circle.FindLineCircleIntersections(ExplodeLocation.Left, ExplodeLocation.Top, EffectiveRange, ray.X1, ray.Y1, ray.X2, ray.Y2, out float ox1, out float oy1, out float ox2, out float oy2);
            var concealedRay = intersectionCount < 1 ? false : LocF.CalculateDistanceTo(ox1, oy1, ray.X1, ray.Y1) < prediction.LKGD;
            concealedRay = concealedRay || (intersectionCount < 2 ? false : LocF.CalculateDistanceTo(ox2, oy2, ray.X1, ray.Y1) < prediction.LKGD);
            concealedRayCount = concealedRay ? concealedRayCount + 1 : concealedRayCount;
        }

        if (concealedRayCount == edgesHit.Count)
        {
            return true;
        }

        // This handles the case where the attacker and the holder are inside of the circle.In that case
        // the rays from hit detection will not intersect with the circle, so we consult our memo table
        // to see if the center of the character is currently blocked.
        var center = c.Center().GetRounded();
        if (reverseMemo.TryGetValue(center, out MemoRecord memoRecord) == false) return false;
        return memoRecord.IsBlocked;
    }

    private async Task DoSmokeAsync()
    {
        ExplodeLocation = alt ? holder.Center().GetFloor() : (await ThrowGrenade()).GetFloor();
        Game.Current.Sound.Play("smoke", EarliestOf(this, holder, holder.GetPropertyValueLifetime(nameof(holder.IsVisible))));
        InitMemo();
        var smokeDuration = TimeSpan.FromSeconds(10);
        var smokeLt = Game.Current.Delay(smokeDuration.TotalMilliseconds).ToLifetime();
        smokeLt.OnDisposed(this.Dispose);
        var smokeStart = Game.Current.MainColliderGroup.Now;
        EffectiveRange = 0;
        while (EffectiveRange < Range)
        {
            EffectiveRange++;
            for (var x = 0; x < memo.Length; x++)
            {
                for (var y = 0; y < memo[x].Length; y++)
                {
                    var memoEntry = memo[x][y];
                    if (memoEntry.IsBlocked) continue;

                    memoEntry.IsBlocked = memoEntry.LOS && memoEntry.DistanceFromCenter <= EffectiveRange;
                    if (memoEntry.IsBlocked)
                    {
                        PlaceSmokeVisual(memoEntry, smokeStart, smokeDuration, smokeLt);
                    }
                }
            }

            await Game.Current.Delay(300);
        }
    }

    private async Task<LocF> ThrowGrenade()
    {
        var grenadeElement = Game.Current.GamePanel.Add(new HackySmokeGrenade(holder));

        var initialPlacement = holder.Center().RadialOffset(holder.Velocity.Angle, 2f);
        grenadeElement.MoveTo(initialPlacement.Left - .5f, initialPlacement.Top - .5f);
        var throwVelocity = grenadeElement.Velocity;
        throwVelocity.Speed = holder.Velocity.Speed + 30;
        throwVelocity.Angle = holder.TargetAngle;
        new Friction(throwVelocity);
        var throwLifetime = Lifetime.EarliestOf(Game.Current.Delay(500).ToLifetime(), throwVelocity.OnCollision.CreateNextFireLifetime());
        await throwLifetime.AsTask();
        grenadeElement.TryDispose();
        return grenadeElement.Center();
    }

    private void InitMemo()
    {
        var obstacles = holder.GetObstacles().Where(o => o is Obstacle).ToList();
        var explodeRect = ExplodeLocation.ToRect(1, 1);
        var memoTopLeftX = ConsoleMath.Round(ExplodeLocation.Left - Range);
        var memoTopLeftY = ConsoleMath.Round(ExplodeLocation.Top - Range);
        reverseMemo = new Dictionary<LocF, MemoRecord>();
        memo = new MemoRecord[2 * (Range + 1)][];
        for (var x = 0; x < memo.Length; x++)
        {
            memo[x] = new MemoRecord[2 * (Range + 1)];
            for (var y = 0; y < memo[x].Length; y++)
            {
                var entry = new MemoRecord() { MemoX = x, MemoY = y };
                memo[x][y] = entry;
                var sceneLocation = new LocF(memoTopLeftX + x, memoTopLeftY + y);
                entry.SceneBounds = new RectF(sceneLocation.Left, sceneLocation.Top, 1, 1);
                reverseMemo.Add(sceneLocation, memo[x][y]);
                entry.DistanceFromCenter = sceneLocation.CalculateNormalizedDistanceTo(ExplodeLocation);
                entry.LOS = CollisionDetector.GetLineOfSightObstruction(explodeRect, entry.SceneBounds, obstacles, CastingMode.SingleRay) == null;
            }
        }
    }

    private async void PlaceSmokeVisual(MemoRecord memoEntry, TimeSpan smokeStart, TimeSpan smokeDuration, ILifetimeManager smokeLt)
    {
        var r = new Random();
        // limit the amount of smoke visuals
        if (r.NextDouble() > .6f) return;

        var smokeElement = Game.Current.GamePanel.Add(new Label("o".ToGray()) { CompositionMode = CompositionMode.BlendBackground });
        smokeElement.MoveTo(memoEntry.SceneBounds.Left, memoEntry.SceneBounds.Top, -1);
        smokeLt.OnDisposed(smokeElement.Dispose);
        while (smokeLt.IsExpired == false)
        {
            var roll = r.NextDouble();
            var display = roll < .99f ? "o".ToGray() : ".".ToGray();
            var durationPercentage = (Game.Current.MainColliderGroup.Now - smokeStart).TotalSeconds / smokeDuration.TotalSeconds;
            var myDistancePercentage = memoEntry.DistanceFromCenter / Range;
            display = Math.Pow(durationPercentage, 10f) > myDistancePercentage && roll < .4f ? "".ToGray() : display;
            smokeElement.Text = display;
            await Task.Yield();
        }
    }
}


public class HackySmokeGrenade : WeaponElement
{
    public HackySmokeGrenade(Character owner) : base(new NoOpWeapon() { Holder = owner })
    {
        CompositionMode = CompositionMode.BlendBackground;
    }
    protected override void OnPaint(ConsoleBitmap context) => context.DrawPoint(new ConsoleCharacter('*', RGB.White), 0, 0);
}
