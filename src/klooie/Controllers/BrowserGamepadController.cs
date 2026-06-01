using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace klooie;

public static class BrowserControllerInput
{
    private static readonly List<BrowserGamepadController> controllers = new();

    public static void Register(BrowserGamepadController controller)
    {
        if (controllers.Contains(controller)) return;
        controllers.Add(controller);
    }

    public static void Unregister(BrowserGamepadController controller) => controllers.Remove(controller);

    public static void UpdateGamepadsJson(string? json)
    {
        for (var i = 0; i < controllers.Count; i++)
        {
            controllers[i].UpdateFromJson(json);
        }
    }
}

public class BrowserGamepadController : Recyclable, IControllerProvider
{
    private static readonly TimeSpan TriggerRepeatInterval = TimeSpan.FromMilliseconds(100);
    private WebGamepadSnapshot? latestSnapshot;
    private int activeIndex = -1;
    private bool wasConnected;
    private bool primed;

    public Controller Controller { get; }
    public bool IsConnected => Controller.IsConnected;
    protected WebGamepadState? ActiveGamepad { get; private set; }

    public BrowserGamepadController()
    {
        Controller = new Controller(this);
        Controller.InitializeFocusStackBindings();
        Controller.SetConnectionState(false);
        BrowserControllerInput.Register(this);
        ConsoleApp.Current?.AfterPaint.Subscribe(this, static me => me.Controller.Update(), this);
    }

    public void UpdateFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            latestSnapshot = null;
            SyncConnectionFromLatestSnapshot();
            return;
        }

        try
        {
            latestSnapshot = JsonSerializer.Deserialize(json, WebGamepadJsonContext.Default.WebGamepadSnapshot);
            SyncConnectionFromLatestSnapshot();
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[BrowserGamepadController] Failed to parse browser gamepad snapshot: {ex.Message}");
        }
    }

    public virtual void Update()
    {
        var gamepad = SelectActiveGamepad();
        var connected = gamepad is not null;
        SyncConnectionState(connected, gamepad);

        if (gamepad is null) return;
        if (primed == false)
        {
            if (HasActiveButtonInput(gamepad) == false)
            {
                PrimeState(gamepad);
            }
            primed = true;
        }

        PollButtons(gamepad);
        PollTriggers(gamepad);
        PollStick(ControllerStickId.Left, gamepad, 0, 1, 10);
        PollStick(ControllerStickId.Right, gamepad, 2, 3, 11);
    }

    public void RumblePulse(float left, float right, ILifetime duration) { }
    public void RumblePulse(float left, float right, float durationms) { }
    public void ForceStopRumble() { }

    private WebGamepadState? SelectActiveGamepad()
    {
        var gamepads = latestSnapshot?.Gamepads;
        if (gamepads is null || gamepads.Length == 0)
        {
            activeIndex = -1;
            ActiveGamepad = null;
            return null;
        }

        if (activeIndex >= 0)
        {
            for (var i = 0; i < gamepads.Length; i++)
            {
                if (gamepads[i].Index == activeIndex && gamepads[i].Connected)
                {
                    ActiveGamepad = gamepads[i];
                    return gamepads[i];
                }
            }
        }

        for (var i = 0; i < gamepads.Length; i++)
        {
            if (gamepads[i].Connected == false) continue;
            activeIndex = gamepads[i].Index;
            ActiveGamepad = gamepads[i];
            Debug.WriteLine($"[BrowserGamepadController] Tracking browser gamepad '{gamepads[i].Id}' mapping='{gamepads[i].Mapping}' index={gamepads[i].Index}");
            return gamepads[i];
        }

        activeIndex = -1;
        ActiveGamepad = null;
        return null;
    }

    private void SyncConnectionFromLatestSnapshot()
    {
        var gamepad = SelectActiveGamepad();
        SyncConnectionState(gamepad is not null, gamepad);
    }

    private void SyncConnectionState(bool connected, WebGamepadState? gamepad)
    {
        if (connected == wasConnected) return;

        wasConnected = connected;
        primed = false;
        Controller.SetConnectionState(connected);
        if (connected == false)
        {
            activeIndex = -1;
            Controller.ResetInputState();
        }
    }

    private void PrimeState(WebGamepadState gamepad)
    {
        PrimeButton(ControllerButtonId.A, gamepad, 0);
        PrimeButton(ControllerButtonId.B, gamepad, 1);
        PrimeButton(ControllerButtonId.X, gamepad, 2);
        PrimeButton(ControllerButtonId.Y, gamepad, 3);
        PrimeButton(ControllerButtonId.LeftBumper, gamepad, 4);
        PrimeButton(ControllerButtonId.RightBumper, gamepad, 5);
        PrimeButton(ControllerButtonId.LeftTrigger, gamepad, 6);
        PrimeButton(ControllerButtonId.RightTrigger, gamepad, 7);
        PrimeButton(ControllerButtonId.View, gamepad, 8);
        PrimeButton(ControllerButtonId.Start, gamepad, 9);
        PrimeButton(ControllerButtonId.DPadUp, gamepad, 12);
        PrimeButton(ControllerButtonId.DPadDown, gamepad, 13);
        PrimeButton(ControllerButtonId.DPadLeft, gamepad, 14);
        PrimeButton(ControllerButtonId.DPadRight, gamepad, 15);
        PrimeButton(ControllerButtonId.Home, gamepad, 16);
        Controller.PrimeStickState(ControllerStickId.Left, ReadStick(gamepad, 0, 1), ReadButton(gamepad, 10));
        Controller.PrimeStickState(ControllerStickId.Right, ReadStick(gamepad, 2, 3), ReadButton(gamepad, 11));
    }

    private void PrimeButton(ControllerButtonId button, WebGamepadState gamepad, int index) => Controller.PrimeButtonState(button, ReadButton(gamepad, index));

    private static bool HasActiveButtonInput(WebGamepadState gamepad)
    {
        var buttons = gamepad.Buttons;
        if (buttons is null) return false;

        for (var i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].Pressed || buttons[i].Value > GamepadInputNormalization.TriggerThreshold) return true;
        }

        return false;
    }

    private void PollButtons(WebGamepadState gamepad)
    {
        PollButton(ControllerButtonId.A, gamepad, 0);
        PollButton(ControllerButtonId.B, gamepad, 1);
        PollButton(ControllerButtonId.X, gamepad, 2);
        PollButton(ControllerButtonId.Y, gamepad, 3);
        PollButton(ControllerButtonId.LeftBumper, gamepad, 4);
        PollButton(ControllerButtonId.RightBumper, gamepad, 5);
        PollButton(ControllerButtonId.View, gamepad, 8);
        PollButton(ControllerButtonId.Start, gamepad, 9);
        PollButton(ControllerButtonId.DPadUp, gamepad, 12);
        PollButton(ControllerButtonId.DPadDown, gamepad, 13);
        PollButton(ControllerButtonId.DPadLeft, gamepad, 14);
        PollButton(ControllerButtonId.DPadRight, gamepad, 15);
        PollButton(ControllerButtonId.Home, gamepad, 16);
    }

    private void PollButton(ControllerButtonId button, WebGamepadState gamepad, int index) => Controller.PollButton(button, ReadButton(gamepad, index));

    private void PollTriggers(WebGamepadState gamepad)
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        Controller.PollTrigger(ControllerButtonId.LeftTrigger, ReadButton(gamepad, 6) || Controller.IsTriggerDriven(ControllerButtonId.LeftTrigger), nowTicks, TriggerRepeatInterval);
        Controller.PollTrigger(ControllerButtonId.RightTrigger, ReadButton(gamepad, 7) || Controller.IsTriggerDriven(ControllerButtonId.RightTrigger), nowTicks, TriggerRepeatInterval);
    }

    private void PollStick(ControllerStickId stick, WebGamepadState gamepad, int xAxis, int yAxis, int pressButton)
        => Controller.PollStick(stick, ReadStick(gamepad, xAxis, yAxis), ReadButton(gamepad, pressButton));

    private static bool ReadButton(WebGamepadState gamepad, int index)
    {
        var buttons = gamepad.Buttons;
        if (buttons is null || index < 0 || index >= buttons.Length) return false;
        return buttons[index].Pressed || buttons[index].Value > GamepadInputNormalization.TriggerThreshold;
    }

    private static LocF ReadStick(WebGamepadState gamepad, int xAxis, int yAxis)
    {
        var axes = gamepad.Axes;
        var x = axes is not null && xAxis >= 0 && xAxis < axes.Length ? axes[xAxis] : 0f;
        var y = axes is not null && yAxis >= 0 && yAxis < axes.Length ? axes[yAxis] : 0f;
        return GamepadInputNormalization.NormalizeWebGamepadStick(x, y);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        BrowserControllerInput.Unregister(this);
        latestSnapshot = null;
        activeIndex = -1;
        wasConnected = false;
        primed = false;
        ActiveGamepad = null;
        Controller.TryDispose("klooie/Controllers/BrowserGamepadController.cs");
    }
}

public sealed class WebGamepadSnapshot
{
    [JsonPropertyName("gamepads")]
    public WebGamepadState[]? Gamepads { get; set; }
}

public sealed class WebGamepadState
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("mapping")]
    public string? Mapping { get; set; }

    [JsonPropertyName("buttons")]
    public WebGamepadButton[]? Buttons { get; set; }

    [JsonPropertyName("axes")]
    public float[]? Axes { get; set; }
}

public sealed class WebGamepadButton
{
    [JsonPropertyName("pressed")]
    public bool Pressed { get; set; }

    [JsonPropertyName("value")]
    public float Value { get; set; }
}

[JsonSerializable(typeof(WebGamepadSnapshot))]
internal sealed partial class WebGamepadJsonContext : JsonSerializerContext
{
}
