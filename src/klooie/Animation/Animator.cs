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
    {
        var deltaBufferR = new float[options.Transitions.Count];
        var deltaBufferG = new float[options.Transitions.Count];
        var deltaBufferB = new float[options.Transitions.Count];

        var deltaBufferRReversed = new float[options.Transitions.Count];
        var deltaBufferGReversed = new float[options.Transitions.Count];
        var deltaBufferBReversed = new float[options.Transitions.Count];

        for (var i = 0; i < options.Transitions.Count; i++)
        {
            deltaBufferR[i] = options.Transitions[i].Value.R - options.Transitions[i].Key.R;
            deltaBufferG[i] = options.Transitions[i].Value.G - options.Transitions[i].Key.G;
            deltaBufferB[i] = options.Transitions[i].Value.B - options.Transitions[i].Key.B;

            deltaBufferRReversed[i] = options.Transitions[i].Key.R - options.Transitions[i].Value.R;
            deltaBufferGReversed[i] = options.Transitions[i].Key.G - options.Transitions[i].Value.G;
            deltaBufferBReversed[i] = options.Transitions[i].Key.B - options.Transitions[i].Value.B;
        }
        var colorBuffer = new RGB[options.Transitions.Count];
        var isReversed = false;
        return AnimateAsync(new FloatAnimationOptions()
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
            Setter = percentage =>
            {
                if (isReversed == false)
                {
                    for (var i = 0; i < options.Transitions.Count; i++)
                    {

                        if (percentage == 1)
                        {
                            colorBuffer[i] = new RGB(
                           (byte)(options.Transitions[i].Value.R),
                           (byte)(options.Transitions[i].Value.G),
                           (byte)(options.Transitions[i].Value.B));
                        }
                        else
                        {
                            var rDeltaFrame = deltaBufferR[i] * percentage;
                            var gDeltaFrame = deltaBufferG[i] * percentage;
                            var bDeltaFrame = deltaBufferB[i] * percentage;

                            colorBuffer[i] = new RGB(
                                (byte)(options.Transitions[i].Key.R + rDeltaFrame),
                                (byte)(options.Transitions[i].Key.G + gDeltaFrame),
                                (byte)(options.Transitions[i].Key.B + bDeltaFrame));
                        }
                    }
                }
                else
                {
                    percentage = 1 - percentage;
                    for (var i = 0; i < options.Transitions.Count; i++)
                    {
                        var rDeltaFrame = deltaBufferRReversed[i] * percentage;
                        var gDeltaFrame = deltaBufferGReversed[i] * percentage;
                        var bDeltaFrame = deltaBufferBReversed[i] * percentage;
                        if (percentage == 1)
                        {
                            colorBuffer[i] = new RGB(
                           (byte)(options.Transitions[i].Key.R),
                           (byte)(options.Transitions[i].Key.G),
                           (byte)(options.Transitions[i].Key.B));
                        }
                        else
                        {
                            colorBuffer[i] = new RGB(
                                (byte)(options.Transitions[i].Value.R + rDeltaFrame),
                                (byte)(options.Transitions[i].Value.G + gDeltaFrame),
                                (byte)(options.Transitions[i].Value.B + bDeltaFrame));
                        }
                    }
                }

                options.OnColorsChanged(colorBuffer);
            }
        });
    }

    public static void AnimateSync(RGBAnimationOptions options)
    {
        var deltaBufferR = new float[options.Transitions.Count];
        var deltaBufferG = new float[options.Transitions.Count];
        var deltaBufferB = new float[options.Transitions.Count];

        var deltaBufferRReversed = new float[options.Transitions.Count];
        var deltaBufferGReversed = new float[options.Transitions.Count];
        var deltaBufferBReversed = new float[options.Transitions.Count];

        for (var i = 0; i < options.Transitions.Count; i++)
        {
            deltaBufferR[i] = options.Transitions[i].Value.R - options.Transitions[i].Key.R;
            deltaBufferG[i] = options.Transitions[i].Value.G - options.Transitions[i].Key.G;
            deltaBufferB[i] = options.Transitions[i].Value.B - options.Transitions[i].Key.B;

            deltaBufferRReversed[i] = options.Transitions[i].Key.R - options.Transitions[i].Value.R;
            deltaBufferGReversed[i] = options.Transitions[i].Key.G - options.Transitions[i].Value.G;
            deltaBufferBReversed[i] = options.Transitions[i].Key.B - options.Transitions[i].Value.B;
        }
        var colorBuffer = new RGB[options.Transitions.Count];
        var isReversed = false;
        AnimateSync(new FloatAnimationOptions()
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
            Setter = percentage =>
            {
                if (isReversed == false)
                {
                    for (var i = 0; i < options.Transitions.Count; i++)
                    {

                        if (percentage == 1)
                        {
                            colorBuffer[i] = new RGB(
                           (byte)(options.Transitions[i].Value.R),
                           (byte)(options.Transitions[i].Value.G),
                           (byte)(options.Transitions[i].Value.B));
                        }
                        else
                        {
                            var rDeltaFrame = deltaBufferR[i] * percentage;
                            var gDeltaFrame = deltaBufferG[i] * percentage;
                            var bDeltaFrame = deltaBufferB[i] * percentage;

                            colorBuffer[i] = new RGB(
                                (byte)(options.Transitions[i].Key.R + rDeltaFrame),
                                (byte)(options.Transitions[i].Key.G + gDeltaFrame),
                                (byte)(options.Transitions[i].Key.B + bDeltaFrame));
                        }
                    }
                }
                else
                {
                    percentage = 1 - percentage;
                    for (var i = 0; i < options.Transitions.Count; i++)
                    {
                        var rDeltaFrame = deltaBufferRReversed[i] * percentage;
                        var gDeltaFrame = deltaBufferGReversed[i] * percentage;
                        var bDeltaFrame = deltaBufferBReversed[i] * percentage;
                        if (percentage == 1)
                        {
                            colorBuffer[i] = new RGB(
                           (byte)(options.Transitions[i].Key.R),
                           (byte)(options.Transitions[i].Key.G),
                           (byte)(options.Transitions[i].Key.B));
                        }
                        else
                        {
                            colorBuffer[i] = new RGB(
                                (byte)(options.Transitions[i].Value.R + rDeltaFrame),
                                (byte)(options.Transitions[i].Value.G + gDeltaFrame),
                                (byte)(options.Transitions[i].Value.B + bDeltaFrame));
                        }
                    }
                }

                options.OnColorsChanged(colorBuffer);
            }
        });
    }


    public static Task AnimateAsync(this ConsoleControl control, ConsoleControlAnimationOptions options)
    {
        var startX = control.Left;
        var startY = control.Top;
        var startW = control.Bounds.Width;
        var startH = control.Bounds.Height;

        return AnimateAsync(new FloatAnimationOptions()
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
            Setter = v =>
            {
                var dest = options.Destination();
                var xDelta = dest.Left - startX;
                var yDelta = dest.Top - startY;
                var wDelta = dest.Width - startW;
                var hDelta = dest.Height - startH;

                var frameX = startX + (v * xDelta);
                var frameY = startY + (v * yDelta);
                var frameW = startW + (v * wDelta);
                var frameH = startH + (v * hDelta);
                var frameBounds = new RectF(frameX, frameY, frameW, frameH);
                options.Setter(control, frameBounds);
            }
        });
    }

    public static void Animate(this ConsoleControl control, ConsoleControlAnimationOptions options)
    {
        var startX = control.Left;
        var startY = control.Top;
        var startW = control.Bounds.Width;
        var startH = control.Bounds.Height;

        AnimateAsync(new FloatAnimationOptions()
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
            Setter = v =>
            {
                var dest = options.Destination();
                var xDelta = dest.Left - startX;
                var yDelta = dest.Top - startY;
                var wDelta = dest.Width - startW;
                var hDelta = dest.Height - startH;

                var frameX = startX + (v * xDelta);
                var frameY = startY + (v * yDelta);
                var frameW = startW + (v * wDelta);
                var frameH = startH + (v * hDelta);
                var frameBounds = new RectF(frameX, frameY, frameW, frameH);
                options.Setter(control, frameBounds);
            }
        });
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
            options.Setter(options.To);
            onDone?.Invoke(scope);
            return;
        }

        var numberOfFrames = (float)(ConsoleMath.Round(animationTime.TotalSeconds * options.TargetFramesPerSecond));
        numberOfFrames = Math.Max(numberOfFrames, 2);

        var timeBetweenFrames = TimeSpan.FromMilliseconds(ConsoleMath.Round(animationTime.TotalMilliseconds / numberOfFrames));

        var initialValue = options.From;
        options.Setter(initialValue);

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
        state.Options.Setter(newValue);

        var delayTime = state.Options.DelayProvider is WallClockDelayProvider ? TimeSpan.FromMilliseconds(Math.Max(0, scheduledTimeAfterThisFrame.TotalMilliseconds - Stopwatch.GetElapsedTime(state.StartTime).TotalMilliseconds)) : state.TimeBetweenFrames;

        if (state.Options.IsCancelled != null && state.Options.IsCancelled())
        {
            state.Dispose();
            return;
        }


        ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(ConsoleMath.Round(delayTime.TotalMilliseconds), state, ProcessAnimationFrame);
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