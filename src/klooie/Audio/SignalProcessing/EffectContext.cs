using System;

namespace klooie;

public struct EffectContext
{
    public float Input;
    public int FrameIndex;
    public float Time;
    public NoteExpression Note;
    public float VelocityNorm => Note.Velocity / 127f;

    public static float EaseLinear(float t) => t;
    public static float EaseInQuad(float t) => t * t;
    public static float EaseOutQuad(float t) => t * (2 - t);
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
    public static float EaseInCubic(float t) => t * t * t;
    public static float EaseOutCubic(float t)
    {
        t -= 1f;
        return t * t * t + 1f;
    }
    public static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;
    }
}

public struct PitchModContext
{
    public float Time;
    public float? ReleaseTime;
    public NoteExpression Note;
}
