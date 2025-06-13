namespace klooie;

public sealed class ConsoleControlAnimationOptions : CommonAnimationOptions
{
    public Func<RectF> Destination { get; set; }
    public void Setter(ConsoleControl target, in RectF bounds) => target.Bounds = bounds;
}
public static partial class Animator
{
    public static Task AnimateAsync(this ConsoleControl control, ConsoleControlAnimationOptions options) => AnimateAsync(CreateConsoleControlAnimation(control, options));

    public static void Animate(this ConsoleControl control, ConsoleControlAnimationOptions options) => AnimateSync(CreateConsoleControlAnimation(control, options));

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

    private sealed class ConsoleControlAnimationState
    {
        public ConsoleControl Control { get; set; }
        public ConsoleControlAnimationOptions Options { get; set; }
        public float StartX { get; set; }
        public float StartY { get; set; }
        public float StartW { get; set; }
        public float StartH { get; set; }
    }
}

