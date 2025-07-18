using System;

namespace klooie;

public struct EffectContext
{
    public float Input;
    public int FrameIndex;
    public float Time;
    public NoteExpression Note;
}

public struct PitchModContext
{
    public float Time;
    public float? ReleaseTime;
    public NoteExpression Note;
}
