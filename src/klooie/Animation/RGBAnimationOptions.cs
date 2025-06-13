using Microsoft.CodeAnalysis;

namespace klooie;

public static partial class Animator
{
    public sealed class RGBAnimationState : FloatAnimationState<RGBAnimationState>
    {
        public List<KeyValuePair<RGB, RGB>> Transitions { get; set; } = new List<KeyValuePair<RGB, RGB>>();
        public Action<RGB[]> OnColorsChanged { get; set; }


        public float[] DeltaR { get; set; }
        public float[] DeltaG { get; set; }
        public float[] DeltaB { get; set; }
        public RGB[] Buffer { get; set; }


        private static LazyPool<RGBAnimationState> pool = new LazyPool<RGBAnimationState>(() => new RGBAnimationState());

        public static RGBAnimationState Create(List<KeyValuePair<RGB, RGB>> Transitions, Action<RGB[]> OnColorsChanged, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = Animator.DeafultTargetFramesPerSecond)
        {
            var ret = pool.Value.Rent();
            ret.Construct(Transitions, OnColorsChanged, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond);
            return ret;
        }

        protected void Construct(List<KeyValuePair<RGB, RGB>> Transitions, Action<RGB[]> OnColorsChanged, double duration, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime loop, Func<bool> isCancelled, int targetFramesPerSecond)
        {
            base.Construct(0, 1, duration, this, SetColors, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond);
            this.Transitions = Transitions ?? throw new ArgumentNullException(nameof(Transitions));
            this.OnColorsChanged = OnColorsChanged ?? throw new ArgumentNullException(nameof(OnColorsChanged));
            DeltaR = new float[Transitions.Count];
            DeltaG = new float[Transitions.Count];
            DeltaB = new float[Transitions.Count];
            for (var i = 0; i < Transitions.Count; i++)
            {
                DeltaR[i] = Transitions[i].Value.R - Transitions[i].Key.R;
                DeltaG[i] = Transitions[i].Value.G - Transitions[i].Key.G;
                DeltaB[i] = Transitions[i].Value.B - Transitions[i].Key.B;
            }
        }

        private void SetColors(RGBAnimationState state, float percentage)
        {
            for (var i = 0; i < state.Buffer.Length; i++)
            {
                var start = state.Transitions[i].Key;
                state.Buffer[i] = new RGB(
                    (byte)(start.R + (state.DeltaR[i] * percentage)),
                    (byte)(start.G + (state.DeltaG[i] * percentage)),
                    (byte)(start.B + (state.DeltaB[i] * percentage)));
            }
            state.OnColorsChanged(state.Buffer);
        }
    }
}