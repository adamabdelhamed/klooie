namespace klooie;

public sealed partial class Controller : Recyclable
{
    public const float HorizontalStickScale = 2f;
    private Event? anyInput;
    private int anyButtonPressedLease;
    private Event? anyButtonPressed;
    private Event<bool>? connectionChanged;
    private Event<ControllerButtonId>? programmaticButtonReleased;
    private readonly IControllerProvider? provider;

    public Controller(IControllerProvider? provider = null)
    {
        this.provider = provider;

        A = new GamePadButton(nameof(A));
        B = new GamePadButton(nameof(B));
        X = new GamePadButton(nameof(X));
        Y = new GamePadButton(nameof(Y));
        Start = new GamePadButton(nameof(Start));
        View = new GamePadButton(nameof(View));
        Home = new GamePadButton(nameof(Home));
        DPadUp = new GamePadButton(nameof(DPadUp));
        DPadDown = new GamePadButton(nameof(DPadDown));
        DPadLeft = new GamePadButton(nameof(DPadLeft));
        DPadRight = new GamePadButton(nameof(DPadRight));
        LeftBumper = new GamePadButton(nameof(LeftBumper));
        RightBumper = new GamePadButton(nameof(RightBumper));
        LeftTrigger = new GamePadButton(nameof(LeftTrigger));
        RightTrigger = new GamePadButton(nameof(RightTrigger));

        LeftStick = new Joystick(nameof(LeftStick));
        RightStick = new Joystick(nameof(RightStick));
    }

    public GamePadButton A { get; }
    public GamePadButton B { get; }
    public GamePadButton X { get; }
    public GamePadButton Y { get; }
    public GamePadButton Start { get; }
    public GamePadButton View { get; }
    public GamePadButton Home { get; }
    public GamePadButton DPadUp { get; }
    public GamePadButton DPadDown { get; }
    public GamePadButton DPadLeft { get; }
    public GamePadButton DPadRight { get; }
    public GamePadButton LeftBumper { get; }
    public GamePadButton RightBumper { get; }
    public GamePadButton LeftTrigger { get; }
    public GamePadButton RightTrigger { get; }
    public Joystick LeftStick { get; }
    public Joystick RightStick { get; }

    public Event AnyInput => anyInput ??= Event.Create();
    public Event AnyButtonPressed
    {
        get
        {
            if(anyButtonPressed != null) return anyButtonPressed;

            anyButtonPressed = Event.Create();
            anyButtonPressedLease = anyButtonPressed.Lease;
            return anyButtonPressed;
        }
    }
    public Event<bool> ConnectionChanged => connectionChanged ??= Event<bool>.Create();
    public Event<ControllerButtonId> ProgrammaticButtonReleased => programmaticButtonReleased ??= Event<ControllerButtonId>.Create();
    public bool IsConnected { get; private set; }

    public void Update()
    {
        if (provider == null) return;

        try
        {
            provider.Update();
        }
        catch (Exception ex)
        {
            TryForceStopProviderRumble();
            SetConnectionState(false);
        }
    }

    public void PrimeButtonState(GamePadButton button, bool isDown) => button.Prime(isDown);
    public void PrimeButtonState(ControllerButtonId button, bool isDown) => PrimeButtonState(GetButton(button), isDown);

    public void PrimeStickState(Joystick stick, LocF value, bool pressed) => stick.Prime(value, pressed);
    public void PrimeStickState(ControllerStickId stick, LocF value, bool pressed) => PrimeStickState(GetStick(stick), value, pressed);

    public static LocF NormalizeStickForConsoleAspectRatio(float x, float y) => new LocF(x * HorizontalStickScale, y);

    public GamePadButton GetButton(ControllerButtonId id) => id switch
    {
        ControllerButtonId.A => A,
        ControllerButtonId.B => B,
        ControllerButtonId.X => X,
        ControllerButtonId.Y => Y,
        ControllerButtonId.Start => Start,
        ControllerButtonId.View => View,
        ControllerButtonId.Home => Home,
        ControllerButtonId.DPadUp => DPadUp,
        ControllerButtonId.DPadDown => DPadDown,
        ControllerButtonId.DPadLeft => DPadLeft,
        ControllerButtonId.DPadRight => DPadRight,
        ControllerButtonId.LeftBumper => LeftBumper,
        ControllerButtonId.RightBumper => RightBumper,
        ControllerButtonId.LeftTrigger => LeftTrigger,
        ControllerButtonId.RightTrigger => RightTrigger,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    public Joystick GetStick(ControllerStickId id) => id switch
    {
        ControllerStickId.Left => LeftStick,
        ControllerStickId.Right => RightStick,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    public void SetConnectionState(bool connected)
    {
        if (connected == IsConnected) return;

        IsConnected = connected;
        if (connected == false)
        {
            ResetInputState();
        }

        ConnectionChanged.Fire(connected);
    }

    public bool PollButton(GamePadButton button, bool isDown)
    {
        var wasDown = button.IsDown;
        var inputDetected = false;
        if (isDown && wasDown == false)
        {
            inputDetected = DispatchButtonPress(button);
            if (inputDetected)
            {
                button.SetDownState(true);
            }
            else
            {
                inputDetected |= button.HandleState(true);
            }
        }
        else
        {
            inputDetected |= button.HandleState(isDown);
        }

        if (inputDetected)
        {
            if (isDown && wasDown == false)
            {
                anyButtonPressed?.Fire();
            }
            AnyInput.Fire();
        }
        return inputDetected;
    }

    public bool PollButton(ControllerButtonId button, bool isDown) => PollButton(GetButton(button), isDown);

    public bool PollStick(Joystick stick, LocF value, bool pressed)
    {
        var inputDetected = stick.HandleInput(value, pressed, suppressMoveEvent: stick.TreatAsDPadRefCount > 0);

        if (stick.TreatAsDPadRefCount > 0)
        {
            inputDetected |= HandleVirtualDPad(stick, value);
        }

        if (inputDetected) AnyInput.Fire();
        return inputDetected;
    }

    public bool PollStick(ControllerStickId stick, LocF value, bool pressed) => PollStick(GetStick(stick), value, pressed);

    public void TreatAsDPad(Joystick stick, ILifetime lt)
    {
        stick.TreatAsDPadRefCount++;
        lt.OnDisposed(() =>
        {
            stick.TreatAsDPadRefCount--;
            if (stick.TreatAsDPadRefCount <= 0)
            {
                stick.TreatAsDPadRefCount = 0;
                stick.LastVirtualDPadButton = null;
            }
        });
    }

    public void TreatAsDPad(ControllerStickId stick, ILifetime lt) => TreatAsDPad(GetStick(stick), lt);

    public void ResetInputState()
    {
        foreach (var button in EnumerateButtons()) button.ResetState();
        LeftStick.ResetState();
        RightStick.ResetState();
    }

    private bool DispatchButtonPress(GamePadButton button)
    {
        if (TryResolveBoundAction(button, out var action))
        {
            action();
            return true;
        }

        return false;
    }

    private bool HandleVirtualDPad(Joystick stick, LocF value)
    {
        var previous = stick.LastVirtualDPadButton;
        var virtualButton = JoystickToVirtualDPad(value, previous, stick);
        stick.LastVirtualDPadButton = virtualButton;

        if (virtualButton == null || ReferenceEquals(previous, virtualButton))
        {
            return false;
        }

        return DispatchButtonPress(virtualButton);
    }

    private GamePadButton? JoystickToVirtualDPad(LocF value, GamePadButton? previous, Joystick stick, float angleOffsetDegrees = -8f)
    {
        const float enterRadius = 0.60f;
        const float releaseRadius = 0.22f;
        const float enterHalfSectorDegrees = 35f;
        const float sameDirectionHoldThreshold = 0.725f;

        var x = value.Left;
        var y = value.Top;
        var magnitude = MathF.Sqrt((x * x) + (y * y));

        if (previous != null)
        {
            if (magnitude < releaseRadius)
            {
                stick.CurrentVirtualDPadPressCommitted = false;
                return null;
            }

            var stronglyHeld = IsStillStronglyHeldInSameDirection(previous, x, y, sameDirectionHoldThreshold);

            if (stick.CurrentVirtualDPadPressCommitted == false)
            {
                if (stronglyHeld)
                {
                    stick.CurrentVirtualDPadPressCommitted = true;
                }

                return previous;
            }

            if (stronglyHeld == false)
            {
                stick.CurrentVirtualDPadPressCommitted = false;
                return null;
            }

            return previous;
        }

        if (magnitude < enterRadius)
        {
            stick.CurrentVirtualDPadPressCommitted = false;
            return null;
        }

        var angle = MathF.Atan2(y, x) * 180f / MathF.PI;
        angle += angleOffsetDegrees;
        angle = NormalizeDegrees(angle);

        GamePadButton? result = null;

        if (IsWithinSector(angle, 0f, enterHalfSectorDegrees)) result = DPadRight;
        else if (IsWithinSector(angle, 90f, enterHalfSectorDegrees)) result = DPadDown;
        else if (IsWithinSector(angle, 180f, enterHalfSectorDegrees)) result = DPadLeft;
        else if (IsWithinSector(angle, 270f, enterHalfSectorDegrees)) result = DPadUp;

        if (result != null)
        {
            stick.CurrentVirtualDPadPressCommitted = IsStillStronglyHeldInSameDirection(result, x, y, sameDirectionHoldThreshold);
        }
        else
        {
            stick.CurrentVirtualDPadPressCommitted = false;
        }

        return result;
    }

    private bool IsStillStronglyHeldInSameDirection(GamePadButton previous, float x, float y, float threshold)
    {
        if (ReferenceEquals(previous, DPadUp)) return y <= -threshold;
        if (ReferenceEquals(previous, DPadDown)) return y >= threshold;
        if (ReferenceEquals(previous, DPadLeft)) return x <= -threshold;
        if (ReferenceEquals(previous, DPadRight)) return x >= threshold;
        return false;
    }

    private static float NormalizeDegrees(float angle)
    {
        while (angle < 0f) angle += 360f;
        while (angle >= 360f) angle -= 360f;
        return angle;
    }

    private static bool IsWithinSector(float angle, float center, float halfWidth)
    {
        return MathF.Abs(ShortestAngleDelta(angle, center)) <= halfWidth;
    }

    private static float ShortestAngleDelta(float a, float b)
    {
        var delta = a - b;
        while (delta <= -180f) delta += 360f;
        while (delta > 180f) delta -= 360f;
        return delta;
    }

    private float ButtonToAngle(GamePadButton button)
    {
        if (button == DPadRight) return 0f;
        if (button == DPadDown) return 90f;
        if (button == DPadLeft) return 180f;
        if (button == DPadUp) return 270f;
        throw new InvalidOperationException("Unsupported virtual d-pad button");
    }

    private ControllerButtonId GetButtonId(GamePadButton button)
    {
        if (ReferenceEquals(button, A)) return ControllerButtonId.A;
        if (ReferenceEquals(button, B)) return ControllerButtonId.B;
        if (ReferenceEquals(button, X)) return ControllerButtonId.X;
        if (ReferenceEquals(button, Y)) return ControllerButtonId.Y;
        if (ReferenceEquals(button, Start)) return ControllerButtonId.Start;
        if (ReferenceEquals(button, View)) return ControllerButtonId.View;
        if (ReferenceEquals(button, Home)) return ControllerButtonId.Home;
        if (ReferenceEquals(button, DPadUp)) return ControllerButtonId.DPadUp;
        if (ReferenceEquals(button, DPadDown)) return ControllerButtonId.DPadDown;
        if (ReferenceEquals(button, DPadLeft)) return ControllerButtonId.DPadLeft;
        if (ReferenceEquals(button, DPadRight)) return ControllerButtonId.DPadRight;
        if (ReferenceEquals(button, LeftBumper)) return ControllerButtonId.LeftBumper;
        if (ReferenceEquals(button, RightBumper)) return ControllerButtonId.RightBumper;
        if (ReferenceEquals(button, LeftTrigger)) return ControllerButtonId.LeftTrigger;
        if (ReferenceEquals(button, RightTrigger)) return ControllerButtonId.RightTrigger;
        throw new ArgumentOutOfRangeException(nameof(button), "Unknown controller button.");
    }

    private IEnumerable<GamePadButton> EnumerateButtons()
    {
        yield return A;
        yield return B;
        yield return X;
        yield return Y;
        yield return Start;
        yield return View;
        yield return Home;
        yield return DPadUp;
        yield return DPadDown;
        yield return DPadLeft;
        yield return DPadRight;
        yield return LeftBumper;
        yield return RightBumper;
        yield return LeftTrigger;
        yield return RightTrigger;
    }

    private void TryForceStopProviderRumble()
    {
        if (provider == null) return;

        try
        {
            provider.ForceStopRumble();
        }
        catch
        {

        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        anyInput?.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:379");
        anyInput = null;
        anyButtonPressed?.Dispose(anyButtonPressedLease, "TotallyTextualBattleSimulator/Controller/Controller.cs:381");
        anyButtonPressed = null;
        connectionChanged?.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:383");
        connectionChanged = null;
        programmaticButtonReleased?.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:385");
        programmaticButtonReleased = null;
        contextStack.Clear();
        contextStack.Add(new InputContext());
        globalContext.ButtonBindings.Clear();
        triggerBindings.Clear();
        triggerHoldBindings.Clear();
        leftTriggerDrivenRefCount = 0;
        rightTriggerDrivenRefCount = 0;
        leftTriggerHandler = null;
        rightTriggerHandler = null;
        leftTriggerHoldStarted = null;
        leftTriggerHoldEnded = null;
        rightTriggerHoldStarted = null;
        rightTriggerHoldEnded = null;
        IsConnected = false;
        A.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:399");
        B.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:400");
        X.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:401");
        Y.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:402");
        Start.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:403");
        View.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:404");
        DPadUp.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:405");
        DPadDown.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:406");
        DPadLeft.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:407");
        DPadRight.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:408");
        LeftBumper.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:409");
        RightBumper.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:410");
        LeftTrigger.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:411");
        RightTrigger.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:412");
        LeftStick.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:413");
        RightStick.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:414");
        Home.TryDispose("TotallyTextualBattleSimulator/Controller/Controller.cs:415");
    }
}
