namespace klooie;

 
public static partial class Animator
{
    public sealed class ConsoleControlAnimationState : FloatAnimationState<ConsoleControlAnimationState>
    {
        public Func<RectF> Destination { get; set; }
        public ConsoleControl Control { get; set; }


        public float StartX { get; set; }
        public float StartY { get; set; }
        public float StartW { get; set; }
        public float StartH { get; set; }

        private static LazyPool<ConsoleControlAnimationState> pool = new LazyPool<ConsoleControlAnimationState>(() => new ConsoleControlAnimationState());

        public static ConsoleControlAnimationState Create(ConsoleControl control, Func<RectF> destination, double duration = 500, EasingFunction easingFunction = null, IDelayProvider delayProvider = null, bool autoReverse = false, float autoReverseDelay = 0, ILifetime loop = null, Func<bool> isCancelled = null, int targetFramesPerSecond = Animator.DeafultTargetFramesPerSecond)
        {
            var ret = pool.Value.Rent();
            ret.Construct(control, destination, duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond);
            return ret;
        }

        protected void Construct(ConsoleControl control, Func<RectF> destination, double duration, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime loop, Func<bool> isCancelled, int targetFramesPerSecond)
        {
            base.Construct(0, 1, duration, this, SetBounds, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, isCancelled, targetFramesPerSecond);
            Control = control ?? throw new ArgumentNullException(nameof(control));
            Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            StartX = control.Left;
            StartY = control.Top;
            StartW = control.Bounds.Width;
            StartH = control.Bounds.Height;
        }

        private static void SetBounds(ConsoleControlAnimationState state, float v)
        {
            var dest = state.Destination();
            var frameBounds = new RectF(
                state.StartX + (v * (dest.Left - state.StartX)),
                state.StartY + (v * (dest.Top - state.StartY)),
                state.StartW + (v * (dest.Width - state.StartW)),
                state.StartH + (v * (dest.Height - state.StartH)));
            state.Control.Bounds = frameBounds;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Control = null;
            Destination = null;
            StartX = 0;
            StartY = 0;
            StartW = 0;
            StartH = 0;
        }
    }
}

