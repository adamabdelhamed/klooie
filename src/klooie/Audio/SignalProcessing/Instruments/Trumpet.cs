using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class Trumpet
{
    public static ISynthPatch Create(NoteExpression note) => SynthPatch.Create(note)
        .WithEnvelope(
            delay: 0,
            attack: t => ADSRCurves.Cubic(t, .005f),
            decay: t => ADSRCurves.Cubic(t, .25f),
            sustainLevel: t => 1f, 
            release: t => ADSRCurves.Linear(t, .1f))
        .WithWaveForm(WaveformType.Saw)
        .WrapWithUnison(6, 15)
        .WithPresenceShelf(new PresenceSettings())
        .WithVolume(1);
}

public static class ADSRCurves
{
    public static double? Instant(double t) => null;
    public static double NoSustain(double t) => 0;


    public static double? Linear(double t, double duration)
    {
        if (t > duration) return null;
        var progress = t / duration; // 0→1
        return progress;
    }

    public static double? Quadratic(double t, double duration)
    {
        if (t > duration) return null;
        var progress = t / duration;
        return progress * progress;
    }

    public static double? Cubic(double t, double duration)
    {
        if (t > duration) return null;
        var progress = t / duration;
        return progress * progress * progress;
    }

    public static double? Power(double t, double duration, int power)
    {
        if (t > duration) return null;
        var progress = t / duration;
        return Math.Pow(progress, power);
    }
}