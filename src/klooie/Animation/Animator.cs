using System.Diagnostics;
using System.Threading.Tasks;
namespace klooie;

/// <summary>
/// An animation utility for async code
/// </summary>
public static class Animator
{
    private static readonly WallClockDelayProvider DefaultDelayProvider = new WallClockDelayProvider();
    internal const int DeafultTargetFramesPerSecond = 20;

    /// <summary>
    /// Performs the animation specified in the options
    /// </summary>
    /// <param name="options">animation options</param>
    /// <returns>an async task</returns>
    public static Task AnimateAsync(FloatAnimationOptions options)
    {
        options.DelayProvider ??= DefaultDelayProvider;
        var state = FloatAnimationState.Create();
        state.Options = options;
        state.OriginalFrom = options.From;
        state.OriginalTo = options.To;
        state.LoopLease = options.Loop?.Lease ?? 0;
        state.Tcs = new TaskCompletionSource();
        var task = state.Tcs.Task;
        FloatAnimationState.StartForwardAnimation(state);
        return task;
    }

    public static void AnimateSync(FloatAnimationOptions options)
    {
        options.DelayProvider ??= DefaultDelayProvider;
        var state = FloatAnimationState.Create();
        state.Options = options;
        state.OriginalFrom = options.From;
        state.OriginalTo = options.To;
        state.LoopLease = options.Loop?.Lease ?? 0;
        FloatAnimationState.StartForwardAnimation(state);
    }

    public static Task AnimateAsync(RGBAnimationOptions options)
        => AnimateAsync(CreateRGBAnimation(options));

    public static void AnimateSync(RGBAnimationOptions options)
        => AnimateSync(CreateRGBAnimation(options));


    public static Task AnimateAsync(this ConsoleControl control, ConsoleControlAnimationOptions options)
        => AnimateAsync(CreateConsoleControlAnimation(control, options));

    public static void Animate(this ConsoleControl control, ConsoleControlAnimationOptions options)
        => AnimateSync(CreateConsoleControlAnimation(control, options));

    private static FloatAnimationOptions<RGBAnimationState> CreateRGBAnimation(RGBAnimationOptions options)
    {
        var state = new RGBAnimationState
        {
            Options = options,
            DeltaR = new float[options.Transitions.Count],
            DeltaG = new float[options.Transitions.Count],
            DeltaB = new float[options.Transitions.Count],
            Buffer = new RGB[options.Transitions.Count]
        };

        for (var i = 0; i < options.Transitions.Count; i++)
        {
            state.DeltaR[i] = options.Transitions[i].Value.R - options.Transitions[i].Key.R;
            state.DeltaG[i] = options.Transitions[i].Value.G - options.Transitions[i].Key.G;
            state.DeltaB[i] = options.Transitions[i].Value.B - options.Transitions[i].Key.B;
        }

        return new FloatAnimationOptions<RGBAnimationState>()
        {
            From = 0,
            To = 1,
            Duration = options.Duration,
            AutoReverse = options.AutoReverse,
            AutoReverseDelay = options.AutoReverseDelay,
            DelayProvider = options.DelayProvider,
            EasingFunction = options.EasingFunction,
            IsCancelled = options.IsCancelled,
            IsPaused = options.IsPaused,
            Loop = options.Loop,
            Target = state,
            Setter = static (s, p) => SetRGBAnimation(s, p)
        };
    }

    private static void SetRGBAnimation(RGBAnimationState state, float percentage)
    {
        for (var i = 0; i < state.Buffer.Length; i++)
        {
            var start = state.Options.Transitions[i].Key;
            state.Buffer[i] = new RGB(
                (byte)(start.R + (state.DeltaR[i] * percentage)),
                (byte)(start.G + (state.DeltaG[i] * percentage)),
                (byte)(start.B + (state.DeltaB[i] * percentage)));
        }
        state.Options.OnColorsChanged(state.Buffer);
    }

    private static FloatAnimationOptions<ConsoleControlAnimationState> CreateConsoleControlAnimation(ConsoleControl control, ConsoleControlAnimationOptions options)
    {
        var state = new ConsoleControlAnimationState
        {
            Control = control,
            Options = options,
            StartX = control.Left,
            StartY = control.Top,
            StartW = control.Bounds.Width,
            StartH = control.Bounds.Height
        };

        return new FloatAnimationOptions<ConsoleControlAnimationState>()
        {
            Duration = options.Duration,
            AutoReverse = options.AutoReverse,
            AutoReverseDelay = options.AutoReverseDelay,
            DelayProvider = options.DelayProvider,
            Loop = options.Loop,
            EasingFunction = options.EasingFunction,
            From = 0,
            To = 1,
            IsCancelled = options.IsCancelled,
            IsPaused = options.IsPaused,
            Target = state,
            Setter = static (s, v) => SetConsoleControlAnimation(s, v)
        };
    }

    private static void SetConsoleControlAnimation(ConsoleControlAnimationState state, float v)
    {
        var dest = state.Options.Destination();
        var frameBounds = new RectF(
            state.StartX + (v * (dest.Left - state.StartX)),
            state.StartY + (v * (dest.Top - state.StartY)),
            state.StartW + (v * (dest.Width - state.StartW)),
            state.StartH + (v * (dest.Height - state.StartH)));
        state.Options.Setter(state.Control, frameBounds);
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

        var frame = AnimationFrameState.Create();
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
        var state = (AnimationFrameState)stateObj;
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


    private sealed class RGBAnimationState
    {
        public RGBAnimationOptions Options { get; set; }
        public float[] DeltaR { get; set; }
        public float[] DeltaG { get; set; }
        public float[] DeltaB { get; set; }
        public RGB[] Buffer { get; set; }
    }

    private sealed class ConsoleControlAnimationState
    {
        public ConsoleControl Control { get; set; }
        public ConsoleControlAnimationOptions Options { get; set; }
        public float StartX { get; set; }
        public float StartY { get; set; }
        public float StartW { get; set; }
        public float StartH { get; set; }
    }

    public class AnimationFrameState : DelayState
    {
        // FloatAnimationOptions options, float numberOfFrames, TimeSpan timeBetweenFrames, float initialValue, float delta, long startTime, float i
        public FloatAnimationOptions Options { get; set; }
        public float NumberOfFrames { get; set; }
        public TimeSpan TimeBetweenFrames { get; set; }
        public float InitialValue { get; set; }
        public float Delta { get; set; }
        public long StartTime { get; set; }
        public float I { get; set; }

        private AnimationFrameState() { }
        private static LazyPool<AnimationFrameState> pool = new LazyPool<AnimationFrameState>(() => new AnimationFrameState());
        public static AnimationFrameState Create()
        {
            var ret = pool.Value.Rent();
            ret.AddDependency(ret);
            return ret;
        }

        protected override void OnInit()
        {
            base.OnInit();
            Options = null;
            NumberOfFrames = 0;
            TimeBetweenFrames = TimeSpan.Zero;
            InitialValue = 0;
            Delta = 0;
            StartTime = 0;
            I = 0;
        }
    }

    public class FloatAnimationState : DelayState
    {
        public FloatAnimationOptions Options { get; set; }
        public float OriginalFrom { get; set; }
        public float OriginalTo { get; set; }
        public int LoopLease { get; set; }
        public TaskCompletionSource? Tcs { get; set; }

        private FloatAnimationState() { }
        private static LazyPool<FloatAnimationState> pool = new LazyPool<FloatAnimationState>(() => new FloatAnimationState());
        public static FloatAnimationState Create()
        {
            var ret = pool.Value.Rent();
            ret.AddDependency(ret);
            return ret;
        }

        protected override void OnInit()
        {
            base.OnInit();
            Options = null;
            OriginalFrom = 0;
            OriginalTo = 0;
            LoopLease = 0;
            Tcs = null;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Options = null;
            OriginalFrom = 0;
            OriginalTo = 0;
            LoopLease = 0;
            Tcs = null;
        }

        // --- Animation sequence helpers ---
        public static void StartForwardAnimation(object stateObj) => StartForwardAnimation((FloatAnimationState)stateObj);
        public static void StartForwardAnimation(FloatAnimationState state)
        {
            Animator.AnimateInternal(state.Options, state, AfterForward);
        }

        private static void AfterForward(object o)
        {
            var state = (FloatAnimationState)o;
            if (state.Options.AutoReverse)
            {
                if (state.Options.AutoReverseDelay > 0)
                {
                    ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.Options.AutoReverseDelay, state, StartReverse);
                }
                else
                {
                    StartReverse(state);
                }
            }
            else
            {
                CompleteOrLoop(state);
            }
        }

        private static void StartReverse(object o)
        {
            var state = (FloatAnimationState)o;
            var temp = state.Options.From;
            state.Options.From = state.Options.To;
            state.Options.To = temp;
            AnimateInternal(state.Options, state, AfterReverse);
        }

        private static void AfterReverse(object o)
        {
            var state = (FloatAnimationState)o;
            if (state.Options.AutoReverseDelay > 0)
            {
                ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.Options.AutoReverseDelay, state, FinishReverse);
            }
            else
            {
                FinishReverse(state);
            }
        }

        private static void FinishReverse(object o)
        {
            var state = (FloatAnimationState)o;
            state.Options.From = state.OriginalFrom;
            state.Options.To = state.OriginalTo;
            CompleteOrLoop(state);
        }

        private static void CompleteOrLoop(FloatAnimationState state)
        {
            if (state.Options.Loop != null && state.Options.Loop.IsStillValid(state.LoopLease))
            {
                StartForwardAnimation(state);
            }
            else
            {
                state.Options.From = state.OriginalFrom;
                state.Options.To = state.OriginalTo;
                state.Tcs?.SetResult();
                state.Dispose();
            }
        }
    }
}