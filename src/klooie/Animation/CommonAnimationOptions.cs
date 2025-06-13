namespace klooie;


public abstract class CommonAnimationState : DelayState
{
    public double Duration { get; set; }
    public EasingFunction EasingFunction { get; set; }
    public IDelayProvider DelayProvider { get; set; }
    public bool AutoReverse { get; set; }
    public ILifetime Loop { get; set; }
    public float AutoReverseDelay { get; set; }
    public Func<bool> IsCancelled { get; set; }
    public int TargetFramesPerSecond { get; set; }

    // Non-Options State
    public int LoopLease { get; set; }
    public TaskCompletionSource? Tcs { get; set; }
    protected CommonAnimationState() { }


    protected void Construct(double duration, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime loop, Func<bool> isCancelled, int targetFramesPerSecond)
    {
        AddDependency(this);
        Duration = duration;
        EasingFunction = easingFunction ?? EasingFunctions.Linear;
        DelayProvider = delayProvider ?? Animator.DefaultDelayProvider;
        AutoReverse = autoReverse;
        AutoReverseDelay = autoReverseDelay;
        Loop = loop;
        LoopLease = loop?.Lease ?? 0;
        IsCancelled = isCancelled;
        TargetFramesPerSecond = targetFramesPerSecond;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        LoopLease = 0;
        DelayProvider = null;
        IsCancelled = null;
        EasingFunction = null;
        Tcs = null;
    }
}