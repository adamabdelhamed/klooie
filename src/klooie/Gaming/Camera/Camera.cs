namespace klooie.Gaming;

/// <summary>
/// A panel that can pan like a camera.
/// </summary>
public sealed partial class Camera : ConsolePanel
{
    private Event cameraLocationChanged;
    public Event CameraLocationChanged => cameraLocationChanged ??= Event.Create();
    private LocF cameraLocation;

    /// <summary>
    /// Gets or sets the camera location. If the BigBounds property has been set then
    /// this property's setter will enforce that the camera stays within the boundaries
    /// defined by BigBounds
    /// </summary>
    public LocF CameraLocation
    {
        get => cameraLocation;
        set
        {
            var proposed = new RectF(value.Left, value.Top, Width, Height);
            var final = EnsureWithinBigBounds(proposed).TopLeft;
            cameraLocation = final;
            CameraLocationChanged.Fire();
            CameraBounds = new RectF(cameraLocation.Left, cameraLocation.Top, Width, Height);
        }
    }

    /// <summary>
    /// Given a rectangle, returns a rectangle that is sure to fit within BigBounds, if BigBounds are set.
    /// </summary>
    /// <param name="given">the rectangle to check</param>
    /// <returns>a rectangle that is sure to fit within BigBounds, if BigBounds are set.</returns>
    public RectF EnsureWithinBigBounds(RectF given)
    {
        if (BigBounds.Width == 0) return given;
        if (given.Width != Width || given.Height != Height) throw new ArgumentException("given rect must be the same size and width as the camera");
        var bounds = BigBounds;

        float x = given.Left < bounds.Left ? bounds.Left : given.Left;
        float y = given.Top < bounds.Top ? bounds.Top : given.Top;

        x = given.Right > bounds.Right ? bounds.Right - given.Width : x;
        y = given.Bottom > bounds.Bottom ? bounds.Bottom - given.Height : y;

        var ret = new RectF(x, y, given.Width, given.Height);
        return ret;
    }

    /// <summary>
    /// Gets the current camera boundaries
    /// </summary>
    public partial RectF CameraBounds { get; set; }

    /// <summary>
    /// Optionally set this property to constrain the camera's movement to an arbitrary rectangle
    /// </summary>
    public RectF BigBounds { get; set; }

    /// <summary>
    /// Points the camera at the location so that it appears at the center of the panel
    /// </summary>
    /// <param name="location">the location to point to</param>
    public void PointAt(LocF location) => CameraLocation = location.Offset(-Bounds.Width / 2f, -Bounds.Height / 2f);

    /// <summary>
    /// Animates the camera to an offset that is relative to its current position
    /// </summary>
    /// <param name="dx">the number of pixels to animate horizontally, can be negative (left) or positive (right)</param>
    /// <param name="dy">the number of pixels to animate vertically, can be negative (up) or positive (down)</param>
    /// <param name="duration">the time in milliseconds to spend on the animation</param>
    /// <param name="ease">the easing function to use</param>
    /// <param name="lt">a lifetime that can be used to cancel the animation</param>
    /// <returns>an async task that completes when the animation is finished or cancelled</returns>
    public Task AnimateBy(float dx, float dy, float duration = 1000, EasingFunction ease = null, ILifetime lt = null) =>
        AnimateTo(CameraLocation.Offset(dx, dy), duration, ease, lt);

    /// <summary>
    /// Animates the camera to the specified centered location 
    /// </summary>
    /// <param name="dest">the desired destination for the camera (top left)</param>
    /// <param name="duration">the time in milliseconds to spend on the animation</param>
    /// <param name="ease">the easing function to use</param>
    /// <param name="lt">a lifetime that can be used to cancel the animation</param>
    /// <param name="delayProvider">the delay provider to use</param>
    /// <returns>an async task that completes when the animation is finished or cancelled</returns>
    public Task PointAnimateTo(LocF dest, float duration = 1000, EasingFunction ease = null, ILifetime lt = null, IDelayProvider delayProvider = null)
    {
        return AnimateTo(dest.Offset(-Width / 2f, -Height / 2f), duration, ease, lt, delayProvider);
    }

    /// <summary>
    /// Animates the camera to the specified location 
    /// </summary>
    /// <param name="dest">the desired destination for the camera (top left)</param>
    /// <param name="duration">the time in milliseconds to spend on the animation</param>
    /// <param name="ease">the easing function to use</param>
    /// <param name="lt">a lifetime that can be used to cancel the animation</param>
    /// <param name="delayProvider">the delay provider to use</param>
    /// <returns>an async task that completes when the animation is finished or cancelled</returns>
    public Task AnimateTo(LocF dest, float duration = 1000, EasingFunction ease = null, ILifetime lt = null, IDelayProvider delayProvider = null)
    {
        ease = ease ?? EasingFunctions.EaseInOut;
        var startX = cameraLocation.Left;
        var startY = cameraLocation.Top;
        var lease = lt?.Lease;
        Action<float> setter = v =>
        {
            var xDelta = dest.Left - startX;
            var yDelta = dest.Top - startY;
            var frameX = startX + (v * xDelta);
            var frameY = startY + (v * yDelta);
            if (lt == null || (lt != null && lt.IsStillValid(lease.Value) == true))
            {
                CameraLocation = new LocF(frameX, frameY);
            }
        };

        return Animator.AnimateAsync(Animator.FloatAnimationState.Create(0,1, duration, setter, ease, delayProvider, false, 0, null, () => lt != null && lt.IsStillValid(lease.Value) == false)); 
    }

    /// <summary>
    /// Registers keyboard handlers with the app so that you can manually pan the camera.
    /// </summary>
    /// <param name="lt">The lifetime of the keyboard registration, defaults to the lifetime of the camera</param>
    /// <param name="wasd">if true, then the WASD keys will be registered for panning</param>
    /// <param name="arrows">if true, then the arrow keys will be registered for panning</param>
    public void EnableKeyboardPanning(ILifetime lt = null, bool wasd = true, bool arrows = true)
    {
        lt = lt ?? this;
        Recyclable panLt = null;
        void animate(float dx, float dy)
        {
            panLt?.Dispose();
            panLt = DefaultRecyclablePool.Instance.Rent();
            AnimateBy(dx, dy, lt: panLt);
        }

        var app = ConsoleApp.Current;

        if (wasd)
        {
            app.PushKeyForLifetime(ConsoleKey.W, () => animate(0, -Height), lt);
            app.PushKeyForLifetime(ConsoleKey.A, () => animate(-Width, 0), lt);
            app.PushKeyForLifetime(ConsoleKey.S, () => animate(0, Height), lt);
            app.PushKeyForLifetime(ConsoleKey.D, () => animate(Width, 0), lt);
            app.PushKeyForLifetime(ConsoleKey.W, ConsoleModifiers.Shift, () => animate(0, -Height / 4), lt);
            app.PushKeyForLifetime(ConsoleKey.A, ConsoleModifiers.Shift, () => animate(-Width / 4, 0), lt);
            app.PushKeyForLifetime(ConsoleKey.S, ConsoleModifiers.Shift, () => animate(0, Height / 4), lt);
            app.PushKeyForLifetime(ConsoleKey.D, ConsoleModifiers.Shift, () => animate(Width / 4, 0), lt);
        }

        if (arrows)
        {
            app.PushKeyForLifetime(ConsoleKey.UpArrow, () => animate(0, -Height), lt);
            app.PushKeyForLifetime(ConsoleKey.LeftArrow, () => animate(-Width, 0), lt);
            app.PushKeyForLifetime(ConsoleKey.DownArrow, () => animate(0, Height), lt);
            app.PushKeyForLifetime(ConsoleKey.RightArrow, () => animate(Width, 0), lt);
            app.PushKeyForLifetime(ConsoleKey.UpArrow, ConsoleModifiers.Shift, () => animate(0, -Height / 4), lt);
            app.PushKeyForLifetime(ConsoleKey.LeftArrow, ConsoleModifiers.Shift, () => animate(-Width / 4, 0), lt);
            app.PushKeyForLifetime(ConsoleKey.DownArrow, ConsoleModifiers.Shift, () => animate(0, Height / 4), lt);
            app.PushKeyForLifetime(ConsoleKey.RightArrow, ConsoleModifiers.Shift, () => animate(Width / 4, 0), lt);
        }
    }

    /// <summary>
    /// This is the secret sauce that enables the camera. The parent panel's composition process
    /// gives derived classes the ability to transform a control's position before composing it
    /// onto its bitmap. We simply subtract the camera position from the control's x and y coordinates
    /// and the rest of the composition just works.
    /// </summary>
    /// <param name="c">the control being composed</param>
    /// <returns>the control coordinates, transformed by the camera position</returns>
    public override (int X, int Y) Transform(ConsoleControl c) =>
        (ConsoleMath.Round(c.Bounds.Left - cameraLocation.Left), ConsoleMath.Round(c.Bounds.Top - cameraLocation.Top));

    /// <summary>
    /// Returns true if the control is within the camera bounds
    /// </summary>
    /// <param name="c">the control to test</param>
    /// <returns>true if the control is within the camera bounds</returns>
    public override bool IsInView(ConsoleControl c) =>
        new RectF(cameraLocation.Left, cameraLocation.Top, Width, Height).Touches(c.Bounds);

    public override bool IsInView(RectF bounds) =>
    new RectF(cameraLocation.Left, cameraLocation.Top, Width, Height).Touches(bounds);

    /// <summary>
    /// When BigBounds is larger than the camera bounds then the camera is free to
    /// move inside of BigBounds. This method calculates the x and y pan coordinates
    /// as a value from 0 to 1. When the camera is all the way to the right then xPer will equal 0.
    /// When the camera is all the way to the left, xPer will equal 1. When the camera is all
    /// the way to the top, yPer will equal 0. When the camera is all the way to the bottom, yPer
    /// will equal 1.
    /// </summary>
    /// <param name="xPer">the horizontal pan position, as a percentage from 0 to 1</param>
    /// <param name="yPer">the vertical pan position, as a percentage from 0 to 1</param>
    public void CalculatePanPercentages(out float xPer, out float yPer)
    {
        var cameraRangeX = BigBounds.Width - Bounds.Width;
        var cameraRangeY = BigBounds.Height - Bounds.Height;

        var xDelta = CameraLocation.Left - BigBounds.Left;
        var yDelta = CameraLocation.Top - BigBounds.Top;

        xPer = cameraRangeX == 0 ? .5f : xDelta / cameraRangeX;
        yPer = cameraRangeY == 0 ? .5f : yDelta / cameraRangeY;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        cameraLocationChanged?.TryDispose();
        cameraLocationChanged = null;
    }
}
