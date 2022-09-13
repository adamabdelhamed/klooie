namespace klooie.Gaming;

public class AutoTargetingOptions
    {
        public Character Source { get; set; }
        public string TargetTag { get; set; }
        public float AngularVisibility { get; set; } = 60;

        public RectF SourceBounds => Source.Bounds;

    }

public class AutoTargetingFunction : Lifetime
{

    public Event<GameCollider> TargetChanged { get; private set; } = new Event<GameCollider>();
    public Event<GameCollider> TargetAcquired { get; private set; } = new Event<GameCollider>();
    public CollisionPrediction TargetCast { get; private set; }
    public AutoTargetingOptions Options { get; private set; }
    private GameCollider lastTarget;
    private List<GameCollider> targets = new List<GameCollider>();

    public IEnumerable<GameCollider> PotentialTargets => targets;

    public float Delay { get; set; }

    private Lifetime currentTargetLifetime;
    public ILifetimeManager CurrentTargetLifetime => currentTargetLifetime == null ? null : Lifetime.EarliestOf(Options.Source, currentTargetLifetime);

    public AutoTargetingFunction(AutoTargetingOptions options)
    {
        this.Options = options;

        Delay = options.Source is MainCharacter ? 50 : 300;
        ConsoleApp.Current.Invoke(async () =>
        {
                // yeild so that the caller can subscribe to events in case 
                // the first evaluation would have triggered one.
                await Task.Yield();
            while (this.IsExpired == false)
            {
                Evaluate();
                await Game.Current.DelayOrYield(Delay);
            }
        });
    }

    private void Evaluate()
    {
        if (Options.Source.IsVisible == false) return;
        var obstacles = Options.Source.GetObstacles().Where(o => o is WeaponElement == false).ToArray();

        GameCollider target = null;
        float winningCandidateProximity = float.MaxValue;
        CollisionPrediction winningPrediction = null;
        targets.Clear();
        foreach (var element in obstacles)
        {
            if (element.CanCollideWith(this.Options.Source) == false && this.Options.Source.CanCollideWith(element) == false) continue;
            if (element.HasSimpleTag(Options.TargetTag) == false) continue;

            if (element is Character && (element as Character).IsVisible == false) continue;

            // todo - give auto targeting function access to the camera in a more structured way
            //if (Game.Current.CameraPanel.CameraBounds.Touches(element.Bounds) == false) continue;

            var sb = Options.SourceBounds;
            var angle = sb.CalculateAngleTo(element.Bounds);
            var delta = Options.Source.Velocity.Angle.DiffShortest(angle);
            if (delta >= Options.AngularVisibility) continue;

            // todo - peek immunity needs to be untangled from this class
            // if (DuckAndCoverAbility.HasPeekImmunity(element as Character, Options.Source, obstacles)) continue;


            var edgesHitOutput = new List<Edge>();

            var prediction = CollisionDetector.Predict(new ColliderBox(sb), angle, obstacles, 3 * Game.Current.GamePanel.Bounds.Hypotenous, CastingMode.Rough);

            var elementHit = prediction.ColliderHit as GameCollider;

            if (elementHit == element)
            {
                // todo - smoke concealment needs to be untangled from this class
                // if (element is Character && SmokeFunction.IsConcealed(element as Character, prediction, edgesHitOutput)) continue;
                targets.Add(elementHit);
                var d = Options.Source.CalculateNormalizedDistanceTo(element);
                if (d < winningCandidateProximity)
                {
                    target = elementHit;
                    winningCandidateProximity = d;
                }
            }
        }


        if (target != lastTarget)
        {
            currentTargetLifetime?.Dispose();
            currentTargetLifetime = target == null ? null : Game.Current.CreateChildLifetime();
            TargetChanged.Fire(target);

            if (target != null)
            {
                TargetCast = winningPrediction;
                TargetAcquired.Fire(target);
            }

            lastTarget = target;
        }
    }
}
