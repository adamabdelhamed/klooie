namespace klooie.Gaming;
public class WanderOptions : MovementOptions
{
    /// <summary>
    /// Point of interest to wander towards, if any. If set, the wanderer will
    /// try to move towards this point which may be an object or an empty space.
    /// 
    /// If not set, the wanderer will wander in the space, trying to avoid objects without
    /// bumping into them. 
    /// 
    /// In all cases, the wanderer will try to avoid turning backwards to avoid obstacles.
    /// Instead it will intelligently alter its trajectory to move around obstacles. That
    /// intelligence is informed by the curiosity point, if set, or by common sense if not.
    /// </summary>
    public Func<ICollidable>? CuriousityPoint { get; set; }

    /// <summary>
    /// When CuriosityPoint is set, the wanderer will stop wandering if it gets within
    /// this distance from the point. 
    /// </summary>
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;

    /// <summary>
    /// Optional code to run before the wanderer is delayed for its next adjustment.
    /// </summary>
    public Action OnDelay { get; set; }
}

/// <summary>
/// A movement that wanders around the space, trying to avoid obstacles and
/// optionally moving towards a point of interest.
/// </summary>
public class Wander : Movement
{
    private const int DelayMs = 50;
    private TaskCompletionSource _tcs;
    public WanderOptions WanderOptions => (WanderOptions)Options;

    public static Movement Create(WanderOptions options)
    {
        var w = WanderPool.Instance.Rent();
        w.Bind(options);
        return w;
    }

    private void Bind(WanderOptions opts)
    {
        base.Bind(opts);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _tcs?.TrySetResult();
        _tcs = null;
    }

    protected override Task Move()
    {
        _tcs = new TaskCompletionSource();
        var state = WanderLoopState.Create(this);
        MoveOnce(state);
        return _tcs.Task;
    }

    private void MoveOnce(WanderLoopState state)
    {
        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }
        WanderLogic.AdjustSpeedAndVelocity(state);

        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }

        ScheduleNextMove(state);
    }

 

    private void ScheduleNextMove(WanderLoopState state)
    {
        ConsoleApp.Current.InnerLoopAPIs.Delay(DelayMs, state, MoveOnceStatic);
    }

    private static void MoveOnceStatic(object o)
    {
        var state = (WanderLoopState)o;
        state.Wander.WanderOptions.OnDelay?.Invoke();
        state.Wander.MoveOnce(state);
    }

    private void Finish()
    {
        _tcs?.TrySetResult();
    }
}

public class WanderLoopState : Recyclable
{
    public Wander Wander { get; private set; } = null!;
    public int WanderLease { get; private set; }
    public int ElementLease { get; private set; }

    /// <summary>
    /// The last few angles the wanderer has taken. This is used to avoid
    /// turning backwards over and over and to help the wanderer make intelligent decisions
    /// </summary>
    public List<Angle> LastFewAngles = new List<Angle>();

    public bool IsStillValid()
        => Wander.IsStillValid(WanderLease)
        && Wander.Options.Velocity?.Collider.IsStillValid(ElementLease) == true;

    public static WanderLoopState Create(Wander w)
    {
        var s = WanderLoopStatePool.Instance.Rent();
        s.Wander = w;
        s.WanderLease = w.Lease;
        s.ElementLease = w.Options.Velocity.Collider.Lease;
        return s;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Wander = null!;
        ElementLease = 0;
        WanderLease = 0;
        LastFewAngles.Clear();
    }
}

/// <summary>
/// Logic for the Wander movement, including speed and velocity adjustments.
/// 
/// Requirements:
/// - The wanderer intelligentlys avoid obstacles by altering its trajectory a few seconds before collision.
/// - The wanderer strongly prefers to walk around obstacles rather than turning around and going back.
/// - If a curiosity point is set, the wanderer should try to move towards it. Speed should be set to zero if the wanderer is close enough to the curiosity point.
/// - If the curiosity point is not set, the wanderer should wander in the space, trying to avoid objects without bumping into them.
/// - All code of non-trivial complexity should be isolated in its own method that accepts the WanderLoopState as a parameter.
///     - All math of non-trivial complexity should have helpful variable names and comments.
/// - Wanderers and obstacles are rectangles that can be any size. The code should never assume a specific size.
/// </summary>
public static class WanderLogic
{
    public static void AdjustSpeedAndVelocity(WanderLoopState state)
    {
        // 1. Select the best steering angle given current situation
        var chosenAngle = SelectSteeringAngle(state);

        // 2. Evaluate the best speed (0 if blocked, full if clear)
        var chosenSpeed = EvaluateSpeed(state, chosenAngle);

        // 3. Apply to velocity
        var velocity = state.Wander.WanderOptions.Velocity;
        velocity.Angle = chosenAngle;
        velocity.Speed = chosenSpeed;

        // 4. Maintain angle history for inertia
        state.LastFewAngles.Add(chosenAngle);
        if (state.LastFewAngles.Count > 10) state.LastFewAngles.RemoveAt(0);
    }

    const float maxDeviation = 45f;
    const float angleStep = 15f;
    const float reactionTime = 0.8f;
    const float inertiaPenalty = 4f;
    const float forwardBonusFactor = 0.75f;
    const float minReward = -10000f;
    const float curiosityBias = 2.0f;

    private static Angle SelectSteeringAngle(WanderLoopState state)
    {
        var options = state.Wander.WanderOptions;
        var velocity = options.Velocity;
        var currentAngle = velocity.Angle;
        var currentSpeed = velocity.Speed;

        Angle? curiosityAngle = null;
        if (options.CuriousityPoint != null)
        {
            var target = options.CuriousityPoint.Invoke();
            if (target != null)
            {
                curiosityAngle = velocity.Collider.Bounds.CalculateAngleTo(target.Bounds);
            }
        }

        // Compute the "inertia angle" (average of recent angles)
        Angle inertiaAngle = currentAngle;
        if (state.LastFewAngles.Count > 0)
        {
            float sum = 0;
            foreach (var angle in state.LastFewAngles)
            {
                sum += angle.Value;
            }
            inertiaAngle = new Angle(sum / state.LastFewAngles.Count);
        }

        var numSteps = (int)(maxDeviation / angleStep);

        var candidateAngles = new List<Angle>();
        for (int i = -numSteps; i <= numSteps; i++)
            candidateAngles.Add(currentAngle.Add(i * angleStep));



        Angle bestAngle = currentAngle;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidateAngles.Count; i++)
        {
            Angle angle = candidateAngles[i];
            float timeToCollision = PredictCollision(state, angle);

            float inertiaDeviation = angle.DiffShortest(inertiaAngle);
            float forwardDeviation = Math.Abs(angle.DiffShortest(currentAngle));

            float curiosityDeviation = 0f;
            if (curiosityAngle.HasValue)
                curiosityDeviation = angle.DiffShortest(curiosityAngle.Value);

            float score;
            if (timeToCollision < reactionTime)
            {
                score = minReward + (timeToCollision - reactionTime) * 10f - inertiaDeviation * inertiaPenalty;
            }
            else
            {
                score = timeToCollision * 2f
                        - inertiaDeviation * inertiaPenalty
                        - forwardDeviation * forwardBonusFactor
                        - curiosityDeviation * curiosityBias;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestAngle = angle;
            }
        }

        return bestAngle;
    }


    private static float EvaluateSpeed(WanderLoopState state, Angle chosenAngle)
    {
        var options = state.Wander.WanderOptions;
        var velocity = options.Velocity;
        var currentSpeed = state.Wander.Options.Speed();

        // If we have a curiosity point, check distance to it
        if (options.CuriousityPoint != null)
        {
            var target = options.CuriousityPoint.Invoke();
            if (target != null)
            {
                float distance = velocity.Collider.Bounds.CalculateDistanceTo(target.Bounds);
                if (distance <= options.CloseEnough)
                    return 0f; // Arrived at curiosity point
            }
        }
        return currentSpeed;
    }

    private static float PredictCollision(WanderLoopState state, Angle angle)
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            for (var i = 0; i < state.Wander.Options.Vision.TrackedObjectsList.Count; i++)
            {
                buffer.WriteableBuffer.Add(state.Wander.Options.Vision.TrackedObjectsList[i].Target);
            }

            CollisionDetector.Predict(state.Wander.Options.Velocity.Collider, angle, buffer.WriteableBuffer, state.Wander.Options.Vision.Range, CastingMode.SingleRay, buffer.WriteableBuffer.Count, prediction);
            if (prediction.CollisionPredicted == false) return float.MaxValue;
            var timeToCollision = prediction.LKGD * state.Wander.Options.Velocity.Speed;
            return timeToCollision;
        }
        finally
        {
            buffer.Dispose();
            prediction.Dispose();
        }
    }
}

