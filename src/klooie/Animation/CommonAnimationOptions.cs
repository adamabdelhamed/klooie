using klooie.Gaming;

namespace klooie;


public abstract class CommonAnimationState : DelayState
{
    public double Duration { get; private set; }
    public EasingFunction EasingFunction { get; private set; }
    public bool AutoReverse { get; private set; }
    private int LoopLease { get; set; }
    public ILifetime Loop { get; private set; }
    public float AutoReverseDelay { get; private set; }

    private int AnimationLifetimeLease { get; set; }
    public ILifetime? AnimationLifetime { get; private set; }
    public bool LoopShouldContinue => Loop != null && Loop.IsStillValid(LoopLease) && AnimationShouldContinue;
    public bool AnimationShouldContinue => AnimationLifetime == null || AnimationLifetime.IsStillValid(AnimationLifetimeLease);
    public PauseManager? PauseManager { get; set; }

    public TaskCompletionSource? Tcs { get; set; }
    protected CommonAnimationState() { }


    protected void Construct(double duration, EasingFunction easingFunction, PauseManager pauseManager, bool autoReverse, float autoReverseDelay, ILifetime loop, ILifetime? animationLifetime)
    {
        AddDependency(this);
        Duration = duration;
        EasingFunction = easingFunction ?? EasingFunctions.Linear;
        AutoReverse = autoReverse;
        AutoReverseDelay = autoReverseDelay;
        Loop = loop;
        LoopLease = loop?.Lease ?? 0;
        AnimationLifetime = animationLifetime;
        AnimationLifetimeLease = animationLifetime?.Lease ?? 0;
        PauseManager = pauseManager;
    }


    protected override void OnReturn()
    {
        base.OnReturn();
        LoopLease = 0;
        AnimationLifetime = null;
        AnimationLifetimeLease = 0;
        EasingFunction = null;
        Tcs = null;
    }
}