using System.Diagnostics;
namespace klooie;

/// <summary>
/// Options for doing animations
/// </summary>
public abstract class AnimatorOptions
    {
        /// <summary>
        /// The starting value of the animated property
        /// </summary>
        public float From { get; set; }
        /// <summary>
        /// The final value of the animated property
        /// </summary>
        public float To { get; set; }
        /// <summary>
        /// The duration of the animation in milliseconds
        /// </summary>
        public double Duration { get; set; } = 500;

        /// <summary>
        /// The easing function to apply
        /// </summary>
        public EasingFunction EasingFunction { get; set; } = Animator.EaseInOut;

        /// <summary>
        /// If true then the animation will automatically reverse itself when done
        /// </summary>
        public bool AutoReverse { get; set; }

        /// <summary>
        /// When specified, the animation will loop until this lifetime completes
        /// </summary>
        public ILifetimeManager Loop { get; set; }

        /// <summary>
        /// The provider to use for delaying between animation frames
        /// </summary>
        public IDelayProvider DelayProvider { get; set; }  

        /// <summary>
        /// If auto reverse is enabled, this is the pause, in milliseconds, after the forward animation
        /// finishes, to wait before reversing
        /// </summary>
        public float AutoReverseDelay { get; set; } = 0;

        /// <summary>
        /// A callback that indicates that we should end the animation early
        /// </summary>
        public Func<bool> IsCancelled { get; set; }

        /// <summary>
        /// A callback that is called before a value is set. The parameter is the percentage done.
        /// </summary>
        public Action<float> OnSet { get; set; }

        /// <summary>
        /// A callback that indicates that the animation should pause
        /// </summary>
        public Func<bool> IsPaused { get; set; }

        internal abstract void Set(float value);

        internal Action<string> Debug { get; set; }

        /// <summary>
        /// A callback that lets you know if the animation is running in reverse (true) or forward (false).
        /// Forward is the default so this will not fire with false unless the animation loops.
        /// </summary>
        public Action<bool> OnReversedChanged { get; set; }

        public async Task<bool> HandlePause()
        {
            var ret = false;
            while (IsPaused != null && IsPaused.Invoke())
            {
                ret = true;
                await Task.Yield();
            }
            return ret;
        }

        public async Task YieldAsync()
        {
            if (await HandlePause() == false)
            {
                await Task.Yield();
            }
        }

        public async Task DelayAsync(TimeSpan ts)
        {
            if (await HandlePause() == false)
            {
                await DelayProvider.Delay(ts);
            }
        }
    }

    /// <summary>
    /// Animation options to use if you are animating a float
    /// </summary>
    public class FloatAnimatorOptions : AnimatorOptions
    {
        /// <summary>
        /// The function that implements changing the value. It will be called throughout the animation process.
        /// </summary>
        public Action<float> Setter { get; set; }

        internal override void Set(float value) => Setter(value);
    }

    /// <summary>
    /// Animation options to use if you are animating an int
    /// </summary>
    public class RoundedAnimatorOptions : AnimatorOptions
    {
        /// <summary>
        /// The function that implements changing the value. It will be called throughout the animation process.
        /// </summary>
        public Action<int> Setter { get; set; }
        internal override void Set(float value) => Setter(ConsoleMath.Round(value));
    }


    public delegate float EasingFunction(float f);

    /// <summary>
    /// An animation utility for async code
    /// </summary>
    public class Animator
    {
        /// <summary>
        /// A linear easing function
        /// </summary>
        /// <param name="percentage">the linear percentage</param>
        /// <returns>the linear percentage</returns>
        public static float Linear(float percentage) => percentage;

        /// <summary>
        /// An easing function that starts slow and accellerates as time moves on
        /// </summary>
        /// <param name="percentage">the linear percentage</param>
        /// <returns>the eased percentage</returns>
        public static float EaseIn(float percentage) => (float)Math.Pow(percentage, 5);

        /// <summary>
        /// An easing function that starts fast and decellerates as time moves on
        /// </summary>
        /// <param name="percentage">the linear percentage</param>
        /// <returns>the eased percentage</returns>
        public static float EaseOut(float percentage) => (float)Math.Pow(percentage, 1.0f / 4);

        /// <summary>
        /// An easing function that starts fast and decellerates as time moves on
        /// </summary>
        /// <param name="percentage">the linear percentage</param>
        /// <returns>the eased percentage</returns>

        public static float EaseOutSoft(float percentage) => (float)Math.Pow(percentage, 1.0f / 2);

        /// <summary>
        /// An easing function that starts and ends slow, but peaks at the midpoint
        /// </summary>
        /// <param name="percentage">the linear percentage</param>
        /// <returns>the eased percentage</returns>
        public static float EaseInOut(float percentage) => percentage < .5 ? 4 * percentage * percentage * percentage : (percentage - 1) * (2 * percentage - 2) * (2 * percentage - 2) + 1;

        private const int TargetFramesPerSecond = 20;
        
        /// <summary>
        /// Performs the animation specified in the options
        /// </summary>
        /// <param name="options">animation options</param>
        /// <returns>an async task</returns>
        public static async Task AnimateAsync(AnimatorOptions options)
        {
            if(options.DelayProvider == null)
            {
                options.DelayProvider = new WallClockDelayProvider();
            }

            var originalFrom = options.From;
            var originalTo = options.To;
            try
            {
                var i = 0;
                while (i == 0 || (options.Loop != null && options.Loop.IsExpired == false))
                {
                    i++;
                    await AnimateAsyncInternal(options);

                    if (options.AutoReverse)
                    {
                        if (options.AutoReverseDelay > 0)
                        {
                            await options.DelayAsync(TimeSpan.FromMilliseconds(options.AutoReverseDelay));
                        }

                        var temp = options.From;
                        options.From = options.To;
                        options.To = temp;
                        options.OnReversedChanged?.Invoke(true);
                        await AnimateAsyncInternal(options);

                        if (options.AutoReverseDelay > 0)
                        {
                            await options.DelayAsync(TimeSpan.FromMilliseconds(options.AutoReverseDelay));
                        }

                        options.From = originalFrom;
                        options.To = originalTo;
                        options.OnReversedChanged?.Invoke(false);
                    }
                }
            }
            finally
            {
                options.From = originalFrom;
                options.To = originalTo;
            }
        }

        internal static Task AnimateAsync(RGB from, RGB to, Action<RGB> setter, float duration = 1000, EasingFunction ease = null, bool autoReverse = false, ILifetimeManager loop = null, IDelayProvider delayProvider = null, float autoReverseDelay = 0, Func<bool> isCancelled = null)
        {
            ease = ease ?? Animator.Linear;
            return AnimateAsync(new RGBAnimationOptions()
            {
                Transitions = new List<KeyValuePair<RGB, RGB>>() { new KeyValuePair<RGB, RGB>(from, to) },
                OnColorsChanged = colors => setter(colors[0]),
                Duration = duration,
                EasingFunction = Animator.EaseIn,
                AutoReverse = autoReverse,
                Loop = loop,
                DelayProvider = delayProvider,
                AutoReverseDelay = autoReverseDelay,
                IsCancelled = isCancelled,

            });
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
            return Animator.AnimateAsync(new FloatAnimatorOptions()
            {
                From = 0,
                To = 1,
                Duration = options.Duration,
                AutoReverse = options.AutoReverse,
                AutoReverseDelay = options.AutoReverseDelay,
                DelayProvider = options.DelayProvider,
                EasingFunction = options.EasingFunction,
                IsCancelled = options.IsCancelled,
                Loop = options.Loop,
                OnReversedChanged = (r) => isReversed = r,
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


        private static async Task AnimateAsyncInternal(AnimatorOptions options)
        {

            var animationTime = TimeSpan.FromMilliseconds(options.Duration);
            if (animationTime == TimeSpan.Zero)
            {
#if DEBUG
                options.Debug?.Invoke("NoOp animation");
#endif

                options.Set(options.To);
            }

            var numberOfFrames = (float)(ConsoleMath.Round(animationTime.TotalSeconds * TargetFramesPerSecond));
            numberOfFrames = Math.Max(numberOfFrames, 2);
#if DEBUG
            options.Debug?.Invoke($"Frames: {numberOfFrames}");
#endif
            var timeBetweenFrames = TimeSpan.FromMilliseconds(ConsoleMath.Round(animationTime.TotalMilliseconds / numberOfFrames));
#if DEBUG
            options.Debug?.Invoke($"Time between frames: {timeBetweenFrames.TotalMilliseconds} ms");
#endif
              var initialValue = options.From;
            options.Set(initialValue);
#if DEBUG
            options.Debug?.Invoke($"InitialValue: {initialValue}");
#endif
            var delta = options.To - initialValue;
#if DEBUG
            options.Debug?.Invoke($"Delta: {delta}");
#endif
            var workSw = Stopwatch.StartNew();
            for(float i = 1; i <= numberOfFrames; i++)
            {
                var percentageDone = i / numberOfFrames;
                if(options.EasingFunction != null)
                {
                    percentageDone = options.EasingFunction(percentageDone);
                }

                var scheduledTimeAfterThisFrame = TimeSpan.FromMilliseconds(timeBetweenFrames.TotalMilliseconds * i);
                var newValue = initialValue + (delta * percentageDone);
                options.OnSet?.Invoke(percentageDone);
                options.Set(newValue);
                ConsoleApp.Current?.RequestPaint();
#if DEBUG
                options.Debug?.Invoke($"Set value to {newValue} at percentage {percentageDone}");
#endif
                var delayTime = options.DelayProvider is WallClockDelayProvider ? TimeSpan.FromMilliseconds(Math.Max(0, scheduledTimeAfterThisFrame.TotalMilliseconds - workSw.Elapsed.TotalMilliseconds)) : timeBetweenFrames;
#if DEBUG
                options.Debug?.Invoke($"Delayed for {delayTime.TotalMilliseconds} ms at percentage {percentageDone}");
#endif

                if(options.IsCancelled != null && options.IsCancelled())
                {
                    return;
                }
                if (delayTime == TimeSpan.Zero)
                {
                    await options.YieldAsync();
                }
                else
                {
                    await options.DelayAsync(delayTime);
                }
            }
        }
    }

    public class RGBAnimationOptions
    {
        public List<KeyValuePair<RGB, RGB>> Transitions { get; set; } = new List<KeyValuePair<RGB, RGB>>();

        public float Duration { get; set; }
        /// <summary>
        /// The easing function to apply
        /// </summary>
        public EasingFunction EasingFunction { get; set; } = Animator.EaseInOut;

        /// <summary>
        /// If true then the animation will automatically reverse itself when done
        /// </summary>
        public bool AutoReverse { get; set; }

        /// <summary>
        /// When specified, the animation will loop until this lifetime completes
        /// </summary>
        public ILifetimeManager Loop { get; set; }

        /// <summary>
        /// The provider to use for delaying between animation frames
        /// </summary>
        public IDelayProvider DelayProvider { get; set; }

        /// <summary>
        /// If auto reverse is enabled, this is the pause, in milliseconds, after the forward animation
        /// finishes, to wait before reversing
        /// </summary>
        public float AutoReverseDelay { get; set; } = 0;

        /// <summary>
        /// A callback that indicates that we should end the animation early
        /// </summary>
        public Func<bool> IsCancelled { get; set; }

        public Action<RGB[]> OnColorsChanged { get; set; }
    }


public class CustomEase
{
    private float[] keyFrames;
    public CustomEase(float[] keyFrames)
    {
        this.keyFrames = keyFrames;
    }

    public float Ease(float percentage)
    {
        var rawIndex = (keyFrames.Length - 1) * percentage;
        if (rawIndex == (int)rawIndex)
        {
            return keyFrames[(int)rawIndex];
        }
        else
        {
            var splitLocation = rawIndex - (int)rawIndex;
            var previousKeyFrame = keyFrames[(int)rawIndex];
            var nextKeyFrame = keyFrames[(int)rawIndex + 1];
            var nextFrameDelta = nextKeyFrame - previousKeyFrame;
            var interpolationAmount = splitLocation * nextFrameDelta;
            return previousKeyFrame + interpolationAmount;
        }
    }
}

public static class ConsoleControlAnimationExtensions
{
    public static Task AnimateForeground(this ConsoleControl control, RGB to, float duration = 1000, EasingFunction ease = null, bool autoReverse = false, ILifetimeManager loop = null, IDelayProvider delayProvider = null, float autoReverseDelay = 0, Func<bool> isCancelled = null)
        => Animator.AnimateAsync(control.Foreground, to, c => control.Foreground = c, duration, ease, autoReverse, loop, delayProvider, autoReverseDelay, isCancelled);

    public static Task AnimateBackground(this ConsoleControl control, RGB to, float duration = 1000, EasingFunction ease = null, bool autoReverse = false, ILifetimeManager loop = null, IDelayProvider delayProvider = null, float autoReverseDelay = 0, Func<bool> isCancelled = null)
        => Animator.AnimateAsync(control.Background, to, c => control.Background = c, duration, ease, autoReverse, loop, delayProvider, autoReverseDelay, isCancelled);
}