using System.Diagnostics;
using System.Threading.Tasks;
namespace klooie;

/// <summary>
/// An animation utility for async code
/// </summary>
public static partial class Animator
{
    private static readonly WallClockDelayProvider DefaultDelayProvider = new WallClockDelayProvider();
    internal const int DeafultTargetFramesPerSecond = 20;

    private static FloatAnimationState CommonStateFactory(FloatAnimationOptions options)
    {
        var state = FloatAnimationState.Create();
        state.Options = options;
        state.OriginalFrom = options.From;
        state.OriginalTo = options.To;
        state.LoopLease = options.Loop?.Lease ?? 0;
        return state;
    }

    public static Task AnimateAsync(FloatAnimationOptions options)
    {
        options.DelayProvider ??= DefaultDelayProvider;
        var state = CommonStateFactory(options);
        state.Tcs = new TaskCompletionSource();
        var task = state.Tcs.Task;
        FloatAnimationState.StartForwardAnimation(state);
        return task;
    }

    public static void AnimateSync(FloatAnimationOptions options)
    {
        options.DelayProvider ??= DefaultDelayProvider;
        var state = CommonStateFactory(options);
        FloatAnimationState.StartForwardAnimation(state);
    }

    private static void AnimateInternal(FloatAnimationOptions options, object scope, Action<object> onDone)
    {
        if (options.IsCancelled != null && options.IsCancelled())
        {
            onDone?.Invoke(scope);
            return;
        }
        var animationTime = TimeSpan.FromMilliseconds(options.Duration);
        if (animationTime == TimeSpan.Zero)
        {
            options.Set(options.To);
            onDone?.Invoke(scope);
            return;
        }

        var numberOfFrames = (float)(ConsoleMath.Round(animationTime.TotalSeconds * options.TargetFramesPerSecond));
        numberOfFrames = Math.Max(numberOfFrames, 2);

        var timeBetweenFrames = TimeSpan.FromMilliseconds(ConsoleMath.Round(animationTime.TotalMilliseconds / numberOfFrames));

        var initialValue = options.From;
        options.Set(initialValue);

        var delta = options.To - initialValue;

        var frame = AnimationState.Create();
        frame.Options = options;
        frame.NumberOfFrames = numberOfFrames;
        frame.TimeBetweenFrames = timeBetweenFrames;
        frame.InitialValue = initialValue;
        frame.Delta = delta;
        frame.StartTime = Stopwatch.GetTimestamp();
        frame.I = -1;
        frame.OnDisposed(scope, onDone);
        ProcessAnimationFrame(frame);
    }

    private static void ProcessAnimationFrame(object stateObj)
    {
        var state = (AnimationState)stateObj;
        if (state.I == state.NumberOfFrames - 1)
        {
            state.Dispose();
            return;
        }
        state.I++;
        var percentageDone = state.I / state.NumberOfFrames;
        if (state.Options.EasingFunction != null)
        {
            percentageDone = state.Options.EasingFunction(percentageDone);
        }

        var scheduledTimeAfterThisFrame = TimeSpan.FromMilliseconds(state.TimeBetweenFrames.TotalMilliseconds * state.I);
        var newValue = state.InitialValue + (state.Delta * percentageDone);
        state.Options.Set(newValue);

        var delayTime = state.Options.DelayProvider is WallClockDelayProvider ? TimeSpan.FromMilliseconds(Math.Max(0, scheduledTimeAfterThisFrame.TotalMilliseconds - Stopwatch.GetElapsedTime(state.StartTime).TotalMilliseconds)) : state.TimeBetweenFrames;

        if (state.Options.IsCancelled != null && state.Options.IsCancelled())
        {
            state.Dispose();
            return;
        }

        ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(ConsoleMath.Round(delayTime.TotalMilliseconds), state, ProcessAnimationFrame);
    }
}

