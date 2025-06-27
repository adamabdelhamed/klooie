using klooie.Gaming;

namespace klooie;

public static partial class Animator
{
    private sealed class CameraAnimationState : FloatAnimationState<CameraAnimationState>
    {
        public Camera Camera { get; set; }
        public LocF Destination { get; set; }
        public float StartX { get; set; }
        public float StartY { get; set; }

        private static LazyPool<CameraAnimationState> pool = new LazyPool<CameraAnimationState>(() => new CameraAnimationState());

        public static CameraAnimationState Create(Camera camera, LocF destination, double duration, EasingFunction easingFunction, SyncronousScheduler scheduler, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime)
        {
            var ret = pool.Value.Rent();
            ret.Construct(camera, destination, duration, easingFunction, scheduler, autoReverse, autoReverseDelay, loop, animationLifetime);
            return ret;
        }

        protected void Construct(Camera camera, LocF destination, double duration, EasingFunction easingFunction, SyncronousScheduler scheduler, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime)
        {
            base.Construct(0, 1, duration, this, SetLocation, easingFunction, scheduler, autoReverse, autoReverseDelay, loop, animationLifetime);
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Destination = destination;
            StartX = camera.CameraLocation.Left;
            StartY = camera.CameraLocation.Top;
        }

        private static void SetLocation(CameraAnimationState state, float v)
        {
            var xDelta = state.Destination.Left - state.StartX;
            var yDelta = state.Destination.Top - state.StartY;
            var frameX = state.StartX + (v * xDelta);
            var frameY = state.StartY + (v * yDelta);
            state.Camera.CameraLocation = new LocF(frameX, frameY);
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Camera = null;
            Destination = default;
            StartX = 0;
            StartY = 0;
        }
    }
}
