namespace klooie.Gaming;

public class WanderOptions
{
    public float AnglePrecision { get; set; } = 45;
    public float Visibility { get; set; } = 8;
    public Func<GameCollider> CuriousityPoint { get; set; }
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;
    public TimeSpan ReactionTime { get; set; } = TimeSpan.FromSeconds(.2);

    public Func<Task> OnDelay { get; set; }

    public static Dictionary<Type, float> DefaultWeights = new Dictionary<Type, float>()
    {
        {  typeof(VisibilitySense), 10f },
        {  typeof(CloserToTargetSense), 3f },
        {  typeof(SimilarToCurrentDirectionSense), 2f },
    };




    public Dictionary<Type, float> Weights { get; set; } = DefaultWeights;
}

public class Wander : Movement
{
    public WanderOptions Options { get; private set; }

    public Angle? _LastGoodAngle { get; set; }
    public Angle _OptimalAngle { get; set; }
    public Lifetime _IterationLifetime { get; set; }
    public IEnumerable<GameCollider> _Obstacles { get; set; }
    public WanderScore _BestScore { get; set; }
    public GameCollider _CuriosityPoint { get; set; }

    public IWanderSense _VisibilitySense { get; set; }
    public IWanderSense _CloserToTargetSense { get; set; }
    public IWanderSense _SimilarToCurrentDirectionSense { get; set; }
    public IWanderSense _ApproachAngleSense { get; set; }
    public bool IsStuck { get; private set; }

    public TimeSpan? LastStuckTime { get; private set; }
    private Wander(Velocity v, SpeedEval speed, WanderOptions options) : base(v, speed)
    {
        this.Options = options ?? new WanderOptions();
    }

    public static Movement Create(Velocity v, SpeedEval speed, WanderOptions options = null) => new Wander(v, speed, options);

    protected override async Task Move()
    {
        _VisibilitySense = new VisibilitySense();
        _CloserToTargetSense = new CloserToTargetSense();
        _SimilarToCurrentDirectionSense = new SimilarToCurrentDirectionSense();


        var innerSpeed = Speed;
        Speed = () =>
        {
            return innerSpeed();
            var baseSpeed = innerSpeed();
            if (_CuriosityPoint == null) return baseSpeed;

            var prox = Element.Bounds.CalculateDistanceTo(_CuriosityPoint.Bounds) / (Options.Visibility * 3f);
            prox = Math.Min(prox, 1);

            if (prox < 1)
            {
                return (baseSpeed * .3f) + (baseSpeed * .7f * prox);
            }
            else
            {
                return baseSpeed;
            }
        };


        try
        {
            while (this.IsExpired == false)
            {
                _BestScore = null;
                _CuriosityPoint = Options.CuriousityPoint?.Invoke();
                Velocity.Stop();
                _IterationLifetime?.Dispose();
                _IterationLifetime = Game.Current.CreateChildLifetime();

                var elementBounds = Element.Bounds;
                _Obstacles = Velocity.GetObstacles().Where(o => o.CalculateDistanceTo(elementBounds) <= Options.Visibility);
                SetOptimalAngle();

                var cpd = _CuriosityPoint == null ? -1f : _CuriosityPoint.CalculateNormalizedDistanceTo(Element.Bounds);
                Angle lkg;
                if (_CuriosityPoint != null && cpd <= Options.CloseEnough)
                {
                    var a = Element.Bounds.Center.CalculateAngleTo(_CuriosityPoint.Center());
                    Element.MoveTo(_CuriosityPoint.Left, _CuriosityPoint.Top);
                    lkg = a;
                    await YieldForVelocityAndDelay();
                }
                else if (_CuriosityPoint != null && HasStraightPath(_CuriosityPoint.Bounds))
                {
                    Velocity.Angle = Element.Bounds.CalculateAngleTo(_CuriosityPoint.Bounds);
                    Velocity.Speed = Speed();
                    lkg = Velocity.Angle;
                    await YieldForVelocityAndDelay();
                }
                else
                {
                    var scores = GetMovementAngleCandidates().Select(a => ScoreThisOption(a)).ToList();
                    WanderScore.NormalizeScores(scores);
                    scores = scores.OrderByDescending(s => s.FinalScore).ToList();
                    _BestScore = scores.First();
                    var loc = elementBounds;
                    Velocity.Angle = _BestScore.Angle;
                    Velocity.Speed = Speed();
                
                    await YieldForVelocityAndDelay();
                    var locNext = Element.Bounds;
                    if (locNext.Equals(loc))
                    {
                        var overlaps = Element.GetObstacles().Where(e => e.Bounds.Touches(Element.Bounds)).ToList();
                        if (overlaps.Any())
                        {
                            Element.NudgeFree(optimalAngle: Velocity.Angle.Opposite());
                        }
                        else
                        {
                            LastStuckTime = Game.Current.MainColliderGroup.Now;
                            IsStuck = true;
                        }
                    }
                    else
                    {
                        IsStuck = false;
                    }
                    lkg = _BestScore.Angle;
                }

                _LastGoodAngle = lkg;
            }
        }
        finally
        {
            if (Velocity.Collider.IsExpired == false)
            {
                Velocity.Stop();
            }

            if (_IterationLifetime != null && _IterationLifetime.IsExpired == false)
            {
                _IterationLifetime.Dispose();
            }
        }
    }

    private async Task YieldForVelocityAndDelay()
    {
        await YieldAsync();
        await AssertAlive();

        if (Options.OnDelay != null)
        {
            await Options.OnDelay();
            await AssertAlive();
        }
        var delay = Velocity.EvalFrequencySeconds * 1025;
         await Game.Current.Delay(delay);
        await AssertAlive();
    }


    private bool HasStraightPath(RectF cp)
    {
        var cpBox = new ColliderBox(cp);
        var a = Element.Bounds.Center.CalculateAngleTo(cp.Center);
        var colliders = _Obstacles.Union(new GameCollider[] { cpBox }).ToArray();
        var visibility = Element.Bounds.CalculateDistanceTo(cp) * 2f;
        var prediction = HitDetection.PredictHit(Element, a, colliders, visibility, CastingMode.Rough);

        var perfect = prediction.ColliderHit == cpBox;
        if (perfect) return true;
        var closeEnough = new RectF(prediction.LKGX, prediction.LKGY, Element.Bounds.Width, Element.Bounds.Height).CalculateDistanceTo(cp) <= Options.CloseEnough;

        return closeEnough;

    }

    private WanderScore ScoreThisOption(Angle angle)
    {
        return new WanderScore()
        {
            Angle = angle,
            Components = new List<ScoreComponent>()
                {
                    _VisibilitySense.Measure(this, angle).WeighIfNotSet(Options.Weights[typeof(VisibilitySense)]),
                    _CloserToTargetSense.Measure(this, angle).WeighIfNotSet(Options.Weights[typeof(CloserToTargetSense)]),
                    _SimilarToCurrentDirectionSense.Measure(this, angle).WeighIfNotSet(Options.Weights[typeof(SimilarToCurrentDirectionSense)]),
                }
        };
    }

    private IEnumerable<Angle> GetMovementAngleCandidates()
    {
        yield return _OptimalAngle;

        Angle effectiveOptimalAngle = (float)(ConsoleMath.Round(_OptimalAngle.Value / Options.AnglePrecision) * Options.AnglePrecision);
        for (var a = 0f; a <= 180; a += Options.AnglePrecision)
        {
            yield return effectiveOptimalAngle.Add(a);
            if (a != 0)
            {
                yield return effectiveOptimalAngle.Add(-a);
            }
        }
    }

    public void SetOptimalAngle()
    {
        var optimalAngle = _LastGoodAngle.HasValue ? _LastGoodAngle.Value : 0;
        if (_CuriosityPoint != null && _CuriosityPoint.Touches(Element.Bounds) == false)
        {
            optimalAngle = Element.Bounds.CalculateAngleTo(_CuriosityPoint.Bounds);
        }

        _OptimalAngle = optimalAngle;
    }
}

