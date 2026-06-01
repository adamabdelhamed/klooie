namespace klooie;

public sealed class GamePadButton : Recyclable
{
    private int pressedLease;
    private int releasedLease;
    private Event? pressed;
    private Event? released;

    internal GamePadButton(string name)
    {
        Name = name;
    }

    public string Name { get; }
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

    public bool IsDown { get; private set; }
    internal long LastRepeatTicks { get; set; }

    internal void Prime(bool isDown)
    {
        IsDown = isDown;
        LastRepeatTicks = 0;
    }

    internal void SetDownState(bool isDown)
    {
        IsDown = isDown;
    }

    internal bool HandleState(bool isDown)
    {
        var inputDetected = false;
        if (isDown && IsDown == false)
        {
            pressed?.Fire();
            inputDetected = true;
        }
        else if (isDown == false && IsDown)
        {
            released?.Fire();
            inputDetected = true;
        }

        IsDown = isDown;
        return inputDetected;
    }

    internal void ResetState()
    {
        IsDown = false;
        LastRepeatTicks = 0;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        pressed?.Dispose(pressedLease, "TotallyTextualBattleSimulator/Controller/GamePadButton.cs:60");
        pressed = null;
        released?.Dispose(releasedLease, "TotallyTextualBattleSimulator/Controller/GamePadButton.cs:62");
        released = null;
        ResetState();
    }
}
