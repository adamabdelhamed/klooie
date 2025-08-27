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

