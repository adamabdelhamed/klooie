namespace klooie;
public sealed class RGBAnimationOptions : CommonAnimationOptions
{
    public List<KeyValuePair<RGB, RGB>> Transitions { get; set; } = new List<KeyValuePair<RGB, RGB>>();
    public Action<RGB[]> OnColorsChanged { get; set; }
}

public static partial class Animator
{
    public static Task AnimateAsync(RGBAnimationOptions options) => AnimateAsync(CreateRGBAnimation(options));
    public static void AnimateSync(RGBAnimationOptions options) => AnimateSync(CreateRGBAnimation(options));

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

    private sealed class RGBAnimationState
    {
        public RGBAnimationOptions Options { get; set; }
        public float[] DeltaR { get; set; }
        public float[] DeltaG { get; set; }
        public float[] DeltaB { get; set; }
        public RGB[] Buffer { get; set; }
    }
}