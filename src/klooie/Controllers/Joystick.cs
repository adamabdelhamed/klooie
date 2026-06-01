namespace klooie;

public sealed class Joystick : Recyclable
{
    private int movedLease;
    private int pressedLease;
    private int releasedLease;
    private int enteredDeadZoneLease;
    private Event<LocF>? moved;
    private Event? pressed;
    private Event? released;
    private Event? enteredDeadZone;

    internal Joystick(string name)
    {
        Name = name;
        LastValue = default;
        IsInDeadZone = true;
    }
    public bool CurrentVirtualDPadPressCommitted { get; set; }
    public string Name { get; }
    public Event<LocF> Moved
    {
        get
        {
            if (moved != null) return moved;
            moved = Event<LocF>.Create();
            movedLease = moved.Lease;
            return moved;
        }
    }

    public Event Pressed
    {
        get
        {
            if (pressed != null) return pressed;
            pressed = Event.Create();
            pressedLease = pressed.Lease;
            return pressed;
        }
    }

    public Event Released
    {
        get
        {
            if (released != null) return released;
            released = Event.Create();
            releasedLease = released.Lease;
            return released;
        }
    }

    public Event EnteredDeadZone
    {
        get
        {
            if (enteredDeadZone != null) return enteredDeadZone;
            enteredDeadZone = Event.Create();
            enteredDeadZoneLease = enteredDeadZone.Lease;
            return enteredDeadZone;
        }
    }

    internal LocF LastValue { get; private set; }
    public bool IsInDeadZone { get; private set; }
    internal bool LastPressed { get; private set; }
    public int TreatAsDPadRefCount { get; internal set; }
    internal GamePadButton? LastVirtualDPadButton { get; set; }

    internal void Prime(LocF value, bool pressed)
    {
        LastValue = value;
        IsInDeadZone = value == default;
        LastPressed = pressed;
        LastVirtualDPadButton = null;
    }

    internal bool HandleInput(LocF value, bool pressed, bool suppressMoveEvent = false)
    {
        var inputDetected = false;
        var isInDeadZone = value == default;

        if (isInDeadZone && IsInDeadZone == false)
        {
            enteredDeadZone?.Fire();
            inputDetected = true;
        }

        IsInDeadZone = isInDeadZone;

        if (isInDeadZone == false && suppressMoveEvent == false)
        {
            moved?.Fire(value);
            inputDetected = true;
        }

        if (pressed && LastPressed == false)
        {
            this.pressed?.Fire();
            inputDetected = true;
        }
        else if (pressed == false && LastPressed)
        {
            released?.Fire();
            inputDetected = true;
        }

        LastPressed = pressed;
        LastValue = value;
        return inputDetected;
    }

    internal void ResetState()
    {
        LastValue = default;
        IsInDeadZone = true;
        LastPressed = false;
        LastVirtualDPadButton = null;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        moved?.Dispose(movedLease, "TotallyTextualBattleSimulator/Controller/Joystick.cs:85");
        moved = null;
        pressed?.Dispose(pressedLease, "TotallyTextualBattleSimulator/Controller/Joystick.cs:87");
        pressed = null;
        released?.Dispose(releasedLease, "TotallyTextualBattleSimulator/Controller/Joystick.cs:89");
        released = null;
        enteredDeadZone?.Dispose(enteredDeadZoneLease, "TotallyTextualBattleSimulator/Controller/Joystick.cs:91");
        enteredDeadZone = null;
        ResetState();
        TreatAsDPadRefCount = 0;
    }
}
