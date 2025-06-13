using System.Diagnostics;
using System.Threading.Tasks;
namespace klooie;

/// <summary>
/// An animation utility for async code
/// </summary>
public static partial class Animator
{
    public static readonly WallClockDelayProvider DefaultDelayProvider = new WallClockDelayProvider();
    internal const int DeafultTargetFramesPerSecond = 20;

    public static Task AnimateAsync(this float from, float to, double duration, Action<float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = DeafultTargetFramesPerSecond)
        => AnimateAsync(FloatAnimationState.Create(from, to, duration, setter,easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled,targetFramesPerSecond));

    public static void Animate(this float from, float to, double duration, Action<float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = DeafultTargetFramesPerSecond)
        => AnimateSync(FloatAnimationState.Create(from, to, duration, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));

    public static Task AnimateAsync<T>(this float from, float to, double duration,T target, Action<T,float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = DeafultTargetFramesPerSecond)
        => AnimateAsync(FloatAnimationState<T>.Create(from, to, duration,target, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));

    public static void Animate<T>(this float from, float to, double duration, T target, Action<T, float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = DeafultTargetFramesPerSecond)
        => AnimateSync(FloatAnimationState<T>.Create(from, to, duration, target, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));

    public static Task AnimateAsync(this ConsoleControl control, Func<RectF> destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = Animator.DeafultTargetFramesPerSecond)
        => AnimateAsync(ConsoleControlAnimationState.Create(control, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));
    public static void AnimateSync(this ConsoleControl control, Func<RectF> destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = Animator.DeafultTargetFramesPerSecond)
     => AnimateSync(ConsoleControlAnimationState.Create(control, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));

    public static Task AnimateAsync(this RGB from, RGB to, double duration, Action<RGB> onColorChanged, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = Animator.DeafultTargetFramesPerSecond)
        => AnimateAsync(RGBAnimationState.Create(from, to, duration, onColorChanged, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));

    public static void AnimateSync(this RGB from, RGB to, double duration, Action<RGB> onColorChanged, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = Animator.DeafultTargetFramesPerSecond)
      => AnimateSync(RGBAnimationState.Create(from, to, duration, onColorChanged, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond));



    private static Task AnimateAsync(FloatAnimationState state)
    {
        state.Tcs = new TaskCompletionSource();
        var task = state.Tcs.Task;
        FloatAnimationState.StartForwardAnimation(state);
        return task;
    }

    private static void AnimateSync(FloatAnimationState state) 
            => FloatAnimationState.StartForwardAnimation(state);
   
    private static void AnimateInternal(FloatAnimationState state, Action<FloatAnimationState> onDone)
    {
        if (state.IsCancelled != null && state.IsCancelled())
        {
            onDone?.Invoke(state);
            return;
        }
        var animationTime = TimeSpan.FromMilliseconds(state.Duration);
        if (animationTime == TimeSpan.Zero)
        {
            state.Set(state.To);
            onDone?.Invoke(state);
            return;
        }

        var numberOfFrames = (float)(ConsoleMath.Round(animationTime.TotalSeconds * state.TargetFramesPerSecond));
        numberOfFrames = Math.Max(numberOfFrames, 2);

        var timeBetweenFrames = TimeSpan.FromMilliseconds(ConsoleMath.Round(animationTime.TotalMilliseconds / numberOfFrames));

        var initialValue = state.From;
        state.Set(initialValue);

        var delta = state.To - initialValue;

        var frame = AnimationFrameState.Create();
        frame.AnimationState = state;
        frame.NumberOfFrames = numberOfFrames;
        frame.TimeBetweenFrames = timeBetweenFrames;
        frame.InitialValue = initialValue;
        frame.Delta = delta;
        frame.StartTime = Stopwatch.GetTimestamp();
        frame.I = -1;
        frame.OnDisposed(state, onDone);
        ProcessAnimationFrame(frame);
    }

    private static void ProcessAnimationFrame(object stateObj)
    {
        var frameState = (AnimationFrameState)stateObj;
        if (frameState.I == frameState.NumberOfFrames - 1)
        {
            frameState.Dispose();
            return;
        }
        frameState.I++;
        var percentageDone = frameState.I / frameState.NumberOfFrames;
        if (frameState.AnimationState.EasingFunction != null)
        {
            percentageDone = frameState.AnimationState.EasingFunction(percentageDone);
        }

        var scheduledTimeAfterThisFrame = TimeSpan.FromMilliseconds(frameState.TimeBetweenFrames.TotalMilliseconds * frameState.I);
        var newValue = frameState.InitialValue + (frameState.Delta * percentageDone);
        frameState.AnimationState.Set(newValue);

        var delayTime = frameState.AnimationState.DelayProvider is WallClockDelayProvider ? TimeSpan.FromMilliseconds(Math.Max(0, scheduledTimeAfterThisFrame.TotalMilliseconds - Stopwatch.GetElapsedTime(frameState.StartTime).TotalMilliseconds)) : frameState.TimeBetweenFrames;

        if (frameState.AnimationState.IsCancelled != null && frameState.AnimationState.IsCancelled())
        {
            frameState.Dispose();
            return;
        }

        ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(ConsoleMath.Round(delayTime.TotalMilliseconds), frameState, ProcessAnimationFrame);
    }
}

