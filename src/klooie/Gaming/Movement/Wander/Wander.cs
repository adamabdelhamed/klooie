using System.Runtime.CompilerServices;

namespace klooie.Gaming;

public class WanderOptions
{
    public float AnglePrecision { get; set; } = 45;
    public float Visibility { get; set; } = 8;
    public Func<ICollidable> CuriousityPoint { get; set; }
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;
    public TimeSpan ReactionTime { get; set; } = TimeSpan.FromSeconds(.2);

    public Action OnDelay { get; set; }

    public static Dictionary<Type, float> DefaultWeights = new Dictionary<Type, float>()
    {
        {  typeof(VisibilitySense), 10f },
        {  typeof(CloserToTargetSense), 3f },
        {  typeof(SimilarToCurrentDirectionSense), 2f },
    };




    public Dictionary<Type, float> Weights { get; set; } = DefaultWeights;
}

public partial class Wander : Movement
{
    public WanderOptions Options { get; private set; }

    internal Angle? _LastGoodAngle { get; set; }
    internal Angle _OptimalAngle { get; set; }
    internal Recyclable _IterationLifetime { get; set; }
    internal ObstacleBuffer _Obstacles { get; set; }
    internal WanderScore _BestScore { get; set; }
    internal ICollidable _CuriosityPoint { get; set; }

    internal IWanderSense _VisibilitySense { get; set; }
    internal IWanderSense _CloserToTargetSense { get; set; }
    internal IWanderSense _SimilarToCurrentDirectionSense { get; set; }
    internal IWanderSense _ApproachAngleSense { get; set; }
    public bool IsStuck { get; private set; }

    public TimeSpan? LastStuckTime { get; private set; }

    private TaskCompletionSource moveTask;

    public Wander()
    {
        _finishBodyDelegate = FinishBody;
    }

    private void Bind(Velocity v, SpeedEval speed, WanderOptions options)
    {
        base.Bind(v, speed);
        this.Options = options ?? new WanderOptions();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        this.Options = null;
        _IterationLifetime?.TryDispose();
    }

    public static Movement Create(Velocity v, SpeedEval speed, WanderOptions options = null)
    {
        var wander = WanderPool.Instance.Rent();
        wander.Bind(v, speed, options);
        return wander;
    }

    protected override Task Move()
    {
        moveTask = new TaskCompletionSource();
        _VisibilitySense = new VisibilitySense();
        _CloserToTargetSense = new CloserToTargetSense();
        _SimilarToCurrentDirectionSense = new SimilarToCurrentDirectionSense();
        var moveLease = Lease;
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

        MoveLoopBody(moveLease);
        return moveTask.Task;
    }

    private void FinalizeMove()
    {
        Velocity?.Stop();

        _IterationLifetime?.TryDispose();
        moveTask.SetResult();
        moveTask = null;
    }
    private RectF elementBounds;
    private Angle lkg;
    private void MoveLoopBody(int moveLease)
    {
        try
        {
            if (this.IsStillValid(moveLease) == false)
            {
                FinalizeMove();
                return;
            }

            PrepareIteration();
            FilterObstacles();
            SetOptimalAngle();

            var cpd = _CuriosityPoint == null ? -1f : _CuriosityPoint.Bounds.CalculateNormalizedDistanceTo(Element.Bounds);
            if (_CuriosityPoint != null && cpd <= Options.CloseEnough)
            {
                AtCuriosityPointBranch();
            }
            else if (_CuriosityPoint != null && HasStraightPath(_CuriosityPoint.Bounds))
            {
                StraightTowardsCuriosityPointBranch();
            }
            else
            {
                ScoringBranch();
            }
            YieldForVelocityAndDelay(moveLease, _finishBodyDelegate);
            HandleBeingStuck(elementBounds);
            _LastGoodAngle = lkg;
        }
        catch (Exception ex)
        {
            moveTask?.SetException(ex);
            moveTask = null;
            return;
        }
        finally
        {
            _Obstacles?.TryDispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StraightTowardsCuriosityPointBranch()
    {
        Velocity.Angle = Element.Bounds.CalculateAngleTo(_CuriosityPoint.Bounds);
        Velocity.Speed = Speed();
        lkg = Velocity.Angle;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AtCuriosityPointBranch()
    {
        var a = Element.Bounds.Center.CalculateAngleTo(_CuriosityPoint.Bounds.Center);
        Element.TryMoveTo(_CuriosityPoint.Bounds.Left, _CuriosityPoint.Bounds.Top);
        lkg = a;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FilterObstacles()
    {
        _Obstacles = ObstacleBufferPool.Instance.Rent();

        Velocity.GetObstacles(_Obstacles);

        for (int i = 0; i < _Obstacles.WriteableBuffer.Count; i++)
        {
            var other = _Obstacles.WriteableBuffer[i];
            if (other.CalculateDistanceTo(elementBounds) > Options.Visibility)
            {
                _Obstacles.WriteableBuffer.Remove(other);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PrepareIteration()
    {
        _BestScore = null;
        _CuriosityPoint = Options.CuriousityPoint?.Invoke();
        Velocity.Stop();
        _IterationLifetime?.TryDispose();
        _IterationLifetime = DefaultRecyclablePool.Instance.Rent();
        elementBounds = Element.Bounds;
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void ScoringBranch()
    {
        var scores = RecyclableListPool<WanderScore>.Instance.Rent(20);
        var stuckTime = IsStuck ? Velocity.Group.Now - LastStuckTime.Value : TimeSpan.Zero; ;
        foreach (var angle in GetMovementAngleCandidates())
        {
            scores.Items.Add(ScoreThisOption(angle, stuckTime));
        }
        WanderScore.NormalizeScores(scores.Items);
        DescendingScoreComparer.SortScores(scores);
        _BestScore = scores.Items[0];
        Velocity.Angle = _BestScore.Angle;
        Velocity.Speed = Speed();
        lkg = _BestScore.Angle;
        for (var i = 0; i < scores.Count; i++)
        {
            scores[i].Dispose();
        }
        scores.Dispose();
    }

    private readonly Action<object> _finishBodyDelegate;

    private void FinishBody(object moveLeaseObj)
    {
        var moveLease = (int)moveLeaseObj;
        var _this = this;
        _this.HandleBeingStuck(_this.elementBounds);
        _this._LastGoodAngle = _this.lkg;
        _this.MoveLoopBody(moveLease);
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void HandleBeingStuck(RectF previousLoction)
    {
        var locNow = Element.Bounds;
        var stuckNow = locNow.CalculateDistanceTo(previousLoction) <= 10 * CollisionDetector.VerySmallNumber;
        if (IsStuck == false && stuckNow)
        {
            LastStuckTime = Game.Current.MainColliderGroup.Now;
            IsStuck = true;
        }
        else if (stuckNow == false)
        {
            IsStuck = false;
            LastStuckTime = null;
        }
    }

    private Action<object> then;
    private object thenState;

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void YieldForVelocityAndDelay(object thenState, Action<object> then)
    {
        this.then = then;
        this.thenState = thenState;
        ConsoleApp.Current.InvokeNextCycle(YieldPartTwo, this);
    }

    private static void YieldPartTwo(object? state)
    {
        var _this = (Wander)state;
        try
        {
            if (_this.Velocity == null) throw new ShortCircuitException();
            _this.Options.OnDelay?.Invoke();
        }
        catch (Exception ex)
        {
            _this.moveTask?.SetException(ex);
            _this.moveTask = null;
            return;
        }
        var delay = _this.Velocity.EvalFrequencySeconds * 1025;
        ConsoleApp.Current.InnerLoopAPIs.Delay(delay, _this, YieldPartThree);
    }

    private static void YieldPartThree(object? state)
    {
        var _this = (Wander)state;
        try
        {
            if (_this.Velocity == null) throw new ShortCircuitException();
            _this.then?.Invoke(_this.thenState);
        }catch(Exception ex)
        {
            _this.moveTask?.SetException(ex);
            _this.moveTask = null;
            return;
        }
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private bool HasStraightPath(RectF cp)
    {
        var cpBox = ColliderBoxPool.Instance.Rent();
        cpBox.Bounds = cp;
        try
        {
            var a = Element.Bounds.Center.CalculateAngleTo(cp.Center);
            ArrayPlusOne<GameCollider> colliders = ArrayPlusOnePool<GameCollider>.Instance.Rent();
            colliders.Bind(_Obstacles.WriteableBuffer,cpBox);
            var prediction = CollisionPredictionPool.Instance.Rent();
            try
            {
                var visibility = Element.Bounds.CalculateDistanceTo(cp) * 2f;
                CollisionDetector.Predict(Element, a, colliders, visibility, CastingMode.Rough, bufferLen: _Obstacles.WriteableBuffer.Count + 1, prediction);

                var perfect = prediction.ColliderHit == cpBox;
                if (perfect) return true;
                var closeEnough = new RectF(prediction.LKGX, prediction.LKGY, Element.Bounds.Width, Element.Bounds.Height).CalculateDistanceTo(cp) <= Options.CloseEnough;
                return closeEnough;
            }
            finally
            {
                colliders.Dispose();
                prediction.Dispose();
            }

        }
        finally
        {
            cpBox.Dispose();
        }

    }

    private WanderScore ScoreThisOption(Angle angle, TimeSpan stuckDuration)
    {
        var ret = WanderScore.Create();
        ret.Angle = angle;
        ret.Components.Items.Add(_VisibilitySense.Measure(this, angle, stuckDuration).WeighIfNotSet(Options.Weights[typeof(VisibilitySense)]));
        ret.Components.Items.Add(_CloserToTargetSense.Measure(this, angle, stuckDuration).WeighIfNotSet(Options.Weights[typeof(CloserToTargetSense)]));
        ret.Components.Items.Add(_SimilarToCurrentDirectionSense.Measure(this, angle, stuckDuration).WeighIfNotSet(Options.Weights[typeof(SimilarToCurrentDirectionSense)]));
        return ret;
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
        if (_CuriosityPoint != null && _CuriosityPoint.Bounds.Touches(Element.Bounds) == false)
        {
            optimalAngle = Element.Bounds.CalculateAngleTo(_CuriosityPoint.Bounds);
        }

        _OptimalAngle = optimalAngle;
    }
}

