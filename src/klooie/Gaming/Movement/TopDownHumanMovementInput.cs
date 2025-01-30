namespace klooie.Gaming;

 public class TopDownHumanMovementInput : Recyclable
{
    public class TopDownHumanMovementInputOptions
    {
        public ConsoleKey UpPrimary { get; init; } = ConsoleKey.W;
        public ConsoleKey DownPrimary { get; init; } = ConsoleKey.S;
        public ConsoleKey LeftPrimary { get; init; } = ConsoleKey.A;
        public ConsoleKey RightPrimary { get; init; } = ConsoleKey.D;
        public ConsoleKey UpSecondary { get; init; } = ConsoleKey.UpArrow;
        public ConsoleKey DownSecondary { get; init; } = ConsoleKey.DownArrow;
        public ConsoleKey LeftSecondary { get; init; } = ConsoleKey.LeftArrow;
        public ConsoleKey RightSecondary { get; init; } = ConsoleKey.RightArrow;
    }

    public class MovementFilterContext
    {
        public GameCollider Puppet { get; private set; }
        internal bool ShouldSuppressMovement { get; set; }
        public void Suppress() => ShouldSuppressMovement = true;
        internal void Reset(GameCollider puppet)
        {
            ShouldSuppressMovement = false;
            Puppet = puppet;
        }
    }
    public TopDownHumanMovementInputOptions Options { get;private set; }
    public GameCollider Puppet { get; private set; }
    private Velocity Velocity => Puppet.Velocity;
    private MovementFilterContext movementFilterContext = new MovementFilterContext();
    private Event<MovementFilterContext>? _movementFilter;
    private static Event<MovementFilterContext>? _globalMovementFilter;

    public Event<MovementFilterContext> GlobalMovementFilter => _globalMovementFilter ?? (_globalMovementFilter = EventPool<MovementFilterContext>.Instance.Rent());
    public Event<MovementFilterContext> MovementFilter => _movementFilter ?? (_movementFilter = EventPool<MovementFilterContext>.Instance.Rent());

    public float Speed { get; set; } = 20;

    protected override void OnInit()
    {
        base.OnInit();
        OnDisposed(this, Cleanup);
    }

    public void Bind(GameCollider puppet, TopDownHumanMovementInputOptions options = null)
    {
        if (Puppet != null) throw new InvalidOperationException($"{nameof(Puppet)} is already bound");
        this.Options = options ?? new TopDownHumanMovementInputOptions();
        BindKeys();
        Puppet = puppet;
        Puppet.OnDisposed(this, DisposeMe);
    }

    private void BindKeys()
    {
        ConsoleModifiers? modifiers = null;
        var scope = this;
        var lt = this;
        ConsoleApp.Current?.PushKeyForLifetime(Options.UpPrimary, modifiers, scope, Up, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.DownPrimary, modifiers, scope, Down, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.LeftPrimary, modifiers, scope, Left, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.RightPrimary, modifiers, scope, Right, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.UpSecondary, modifiers, scope, Up, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.DownSecondary, modifiers, scope, Down, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.LeftSecondary, modifiers, scope, Left, lt);
        ConsoleApp.Current?.PushKeyForLifetime(Options.RightSecondary, modifiers, scope, Right, lt);
    }

    private static void Up(object me, ConsoleKeyInfo info) => (me as TopDownHumanMovementInput)!.OnMove(Angle.Up);
    private static void Down(object me, ConsoleKeyInfo info) => (me as TopDownHumanMovementInput)!.OnMove(Angle.Down);
    private static void Left(object me, ConsoleKeyInfo info) => (me as TopDownHumanMovementInput)!.OnMove(Angle.Left);
    private static void Right(object me, ConsoleKeyInfo info) => (me as TopDownHumanMovementInput)!.OnMove(Angle.Right);
    private static void DisposeMe(object me) => (me as TopDownHumanMovementInput)!.TryDispose();

    private static void Cleanup(object me)
    {
        TopDownHumanMovementInput _this = (me as TopDownHumanMovementInput)!;
        if (_this._movementFilter != null) _this._movementFilter.Dispose();
        _this._movementFilter = null;
        _this.Puppet = null;
    }

    protected virtual void OnMove(Angle direction)
    {
        if (Puppet == null) throw new InvalidOperationException($"{nameof(Puppet)} cannot be null");
        if (Puppet.IsVisible == false) return;
        movementFilterContext.Reset(Puppet);
        _movementFilter?.Fire(movementFilterContext);
        if (movementFilterContext.ShouldSuppressMovement) return;

        MoveDirection(direction);
    }

    private void MoveDirection(Angle desiredAngle)
    {
        var currentAngle = Velocity.Angle.RoundAngleToNearest(90);

        if (currentAngle != desiredAngle)
        {
            Velocity.Angle = desiredAngle;
            Velocity.Speed = Math.Min(Velocity.Speed, Speed);
            RoundOff();
        }
        else if (currentAngle == desiredAngle && Velocity.Speed > 0)
        {
            Velocity.Speed = 0;
            Velocity.Angle = desiredAngle;
        }
        else
        {
            Velocity.Angle = desiredAngle;
            Velocity.Speed = Speed;
            RoundOff();
        }
    }

    private void RoundOff()
    {
        var newLeft = (float)ConsoleMath.Round(Puppet.Left);
        var newTop = (float)ConsoleMath.Round(Puppet.Top);
        Puppet.TryMoveTo(newLeft, newTop);
    }
}