namespace klooie;

public static class GamepadInputNormalization
{
    public const float TriggerThreshold = 8f / 255f;
    private const float StickDeadZone = 0.24f;

    public static LocF NormalizeXInputStick(short xValue, short yValue)
        => NormalizeStick(xValue / 32767f, -(yValue / 32767f));

    public static LocF NormalizeWindowsGamingInputStick(double xValue, double yValue)
        => NormalizeStick((float)xValue, -(float)yValue);

    public static LocF NormalizeSdlStick(short xValue, short yValue)
        => NormalizeStick(
            RemapAfterDeadZone(xValue / 32767f),
            RemapAfterDeadZone(yValue / 32767f),
            alreadyDeadZoneAdjusted: true);

    public static LocF NormalizeWebGamepadStick(float xValue, float yValue)
        => NormalizeStick(
            RemapAfterDeadZone(xValue),
            RemapAfterDeadZone(yValue),
            alreadyDeadZoneAdjusted: true);

    private static LocF NormalizeStick(float xValue, float yValue, bool alreadyDeadZoneAdjusted = false)
    {
        return Controller.NormalizeStickForConsoleAspectRatio(
            alreadyDeadZoneAdjusted ? Math.Clamp(xValue, -1f, 1f) : ApplyDeadZone(xValue),
            alreadyDeadZoneAdjusted ? Math.Clamp(yValue, -1f, 1f) : ApplyDeadZone(yValue));
    }

    private static float ApplyDeadZone(float value)
    {
        value = Math.Clamp(value, -1f, 1f);
        return value > -StickDeadZone && value < StickDeadZone ? 0f : value;
    }

    private static float RemapAfterDeadZone(float value)
    {
        value = Math.Clamp(value, -1f, 1f);
        var sign = MathF.Sign(value);
        var magnitude = MathF.Abs(value);

        if (magnitude < StickDeadZone)
        {
            return 0f;
        }

        var normalizedMagnitude = (magnitude - StickDeadZone) / (1f - StickDeadZone);
        return Math.Clamp(normalizedMagnitude * sign, -1f, 1f);
    }
}
