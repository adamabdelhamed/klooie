using Microsoft.CodeAnalysis;

namespace klooie;

public static partial class Animator
{
    private sealed class RGBAnimationState : FloatAnimationState<RGBAnimationState>
    {
        public RGB From { get; set; }
        public RGB To { get; set; }
        public Action<RGB> OnColorChanged { get; set; }

        public float DeltaR { get; set; }
        public float DeltaG { get; set; }
        public float DeltaB { get; set; }

        private static LazyPool<RGBAnimationState> pool = new LazyPool<RGBAnimationState>(() => new RGBAnimationState());

        public static RGBAnimationState Create(RGB from, RGB to, double duration, Action<RGB> onColorChanged , EasingFunction easingFunction, SyncronousScheduler scheduler, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime)
        {
            var ret = pool.Value.Rent();
            ret.Construct(from, to, onColorChanged, duration, easingFunction, scheduler, autoReverse, autoReverseDelay, loop, animationLifetime);
            return ret;
        }

        protected void Construct(RGB from, RGB to,Action<RGB> onColorChanged, double duration, EasingFunction easingFunction, SyncronousScheduler scheduler, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime)
        {
            base.Construct(0, 1, duration, this, SetColors, easingFunction, scheduler, autoReverse, autoReverseDelay, loop, animationLifetime);
            this.From = from;
            this.To = to;
            this.OnColorChanged = onColorChanged ?? throw new ArgumentNullException(nameof(onColorChanged));
           
            DeltaR = To.R - From.R;
            DeltaG = To.G - From.G;
            DeltaB = To.B - From.B;
        }

        private void SetColors(RGBAnimationState state, float percentage) => state.OnColorChanged(
            new RGB((byte)(From.R + DeltaR * percentage),
                    (byte)(From.G + DeltaG * percentage),
                    (byte)(From.B + DeltaB * percentage)));
        
    }
}