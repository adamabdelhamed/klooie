using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class Bass
{
    public static ISynthPatch Basic(NoteExpression note) => SynthPatch.Create(note)
        .WithEnvelope(.005f, 0f, 1f, .0025f)
        .WithWaveForm(WaveformType.Sine)
        .WithDistortion(6)
        .WithVolume(2);
}
