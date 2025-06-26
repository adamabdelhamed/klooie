using System.Diagnostics;
using System.Threading.Tasks;
namespace klooie;

/// <summary>
/// An animation utility for async code
/// </summary>
public static partial class Animator
{

    public static Task AnimateAsync(this float from, float to, double duration, Action<float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateAsync(FloatAnimationState.Create(from, to, duration, setter,easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static ILifetime Animate(this float from, float to, double duration, Action<float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateSync(FloatAnimationState.Create(from, to, duration, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static Task AnimateAsync<T>(this float from, float to, double duration,T target, Action<T,float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateAsync(FloatAnimationState<T>.Create(from, to, duration,target, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static ILifetime Animate<T>(this float from, float to, double duration, T target, Action<T, float> setter, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateSync(FloatAnimationState<T>.Create(from, to, duration, target, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static Task AnimateAsync(this ConsoleControl control, Func<RectF> destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateAsync(ConsoleControlAnimationState.Create(control, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));
    public static ILifetime AnimateSync(this ConsoleControl control, Func<RectF> destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
     => AnimateSync(ConsoleControlAnimationState.Create(control, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static Task AnimateAsync(this Gaming.Camera camera, LocF destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateAsync(CameraAnimationState.Create(camera, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static ILifetime AnimateSync(this Gaming.Camera camera, LocF destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateSync(CameraAnimationState.Create(camera, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static Task AnimateAsync(this RGB from, RGB to, double duration, Action<RGB> onColorChanged, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
        => AnimateAsync(RGBAnimationState.Create(from, to, duration, onColorChanged, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    public static ILifetime AnimateSync(this RGB from, RGB to, double duration, Action<RGB> onColorChanged, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime? loop = null, ILifetime? animationLifetime = null)
      => AnimateSync(RGBAnimationState.Create(from, to, duration, onColorChanged, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime));

    private static Task AnimateAsync(FloatAnimationState state)
    {
        state.Tcs = new TaskCompletionSource();
        var task = state.Tcs.Task;
        FloatAnimationState.StartForwardAnimation(state);
        return task;
    }

    private static ILifetime AnimateSync(FloatAnimationState state) 
            => FloatAnimationState.StartForwardAnimation(state);
   
    private static void AnimateInternal(FloatAnimationState state, Action<FloatAnimationState> onDone)
    {
        if (state.AnimationShouldContinue == false)
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

        var numberOfFrames = (float)(ConsoleMath.Round(animationTime.TotalSeconds * LayoutRootPanel.MaxPaintRate));
        numberOfFrames = Math.Max(numberOfFrames, 2);

        var initialValue = state.From;
        state.Set(initialValue);

        var delta = state.To - initialValue;

        var frame = AnimationFrameState.Create();
        frame.AnimationState = state;
        frame.NumberOfFrames = numberOfFrames;
        frame.InitialValue = initialValue;
        frame.Delta = delta;
        frame.StartTime = Stopwatch.GetTimestamp();
        frame.I = -1;
        frame.OnDisposed(state, onDone);
        ConsoleApp.Current.AfterPaint.Subscribe(frame, static f => ProcessAnimationFrame(f), frame);
        ProcessAnimationFrame(frame);
    }

    private static void ProcessAnimationFrame(AnimationFrameState frameState)
    {
        if (frameState.AnimationState.AnimationShouldContinue == false)
        {
            frameState.Dispose();
            return;
        }

        if (frameState.I == frameState.NumberOfFrames - 1)
        {
            frameState.Dispose();
            return;
        }
        frameState.I++;

        FrameDebugger.RegisterTask(nameof(ProcessAnimationFrame));

        var percentageDone = frameState.I / frameState.NumberOfFrames;
        percentageDone = frameState.AnimationState.EasingFunction != null ? frameState.AnimationState.EasingFunction(percentageDone) : percentageDone;

        var newValue = frameState.InitialValue + (frameState.Delta * percentageDone);
        frameState.AnimationState.Set(newValue);
    }
}

