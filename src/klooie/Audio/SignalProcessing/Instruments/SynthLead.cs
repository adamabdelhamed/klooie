using System.Collections.Generic;
using PowerArgs;
using System;

namespace klooie;

[SynthCategory("Lead")]
[SynthDocumentation("""
Wide, aggressive synth lead designed for heroic melodies. Combines detuned saw stacks,
harmonic square layers and octave-sub reinforcement with time-synced modulation,
chorus, delay and reverb for an epic, evolving tone.
""")]
public static class SynthLead
{
    public static ISynthPatch Create(NoteExpression note)
    {
        return LayeredPatch.CreateBuilder()
         .AddLayer
         (
             volume: 0.9f, pan: -0.4f, transpose: 0, patch: SynthPatch.Create(note)
             .WithWaveForm(WaveformType.Saw)
             .WithVolume(.03f)
             .WithEnvelope(delay: 0, attack: 0.002, decay: 0.07, sustainLevel: 0.95, release: 0.22)
             .WithDCBlocker()
             .WithPitchDrift(0.75f, 6f)
             .WrapWithUnison(numVoices: 4, detuneCents: 28f, panSpread: 1.0f)
             .WrapWithPowerChord([0,7,12])
             .WithChorus(delayMs: 24, depthMs: 6, rateHz: 0.28f, mix: 0.22f)
             .WithPingPongDelay(delayMs: 360, feedback: 0.42f, mix: 0.33f)
             .WithReverb(duration: .2f, wet: .1f, dry: .8f, damping: .2f)
        )
        .Build();
    }
}
