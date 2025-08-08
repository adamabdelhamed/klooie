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
    public static ISynthPatch Create()
    {
        return LayeredPatch.CreateBuilder()
         .AddLayer(volume: 0.9f, pan: -0.4f, transpose: 0, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Saw)
             .WithEnvelope(0.004, 0.09, 0.95, 0.22)
             .WithDCBlocker()
             .WithPitchDrift(0.25f, 4f)
             .WrapWithUnison(numVoices: 5, detuneCents: 14f, panSpread: 1.0f))
         .AddLayer(volume: 0.7f, pan: 0.4f, transpose: 12, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Square)
             .WithEnvelope(0.003, 0.11, 0.90, 0.2)
             .WithSubOscillator(subOscLevel: 0.5f, subOscOctaveOffset: -1)
             .WithDCBlocker()
             .WithPitchDrift(0.2f, 3f)
             .WithPeakEQRelative(0.5f, +3.5f, 0.7f)
             .WithLowPassRelative(2.8f)
             .WrapWithUnison(numVoices: 4, detuneCents: 10f, panSpread: 0.9f))
         .AddLayer(volume: 0.5f, pan: 0.0f, transpose: -12, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Saw)
             .WithEnvelope(0.005, 0.10, 0.85, 0.22)
             .WithPresenceShelf(4.0f))

         .Build()

         // Global effects chain
         .WithChorus(delayMs: 24, depthMs: 6, rateHz: 0.28f, mix: 0.22f)
         .WithPingPongDelay(delayMs: 360, feedback: 0.42f, mix: 0.33f)
         .WithReverb(duration: .3f, wet: .05f)
         .WithVolume(0.03f);
    }
}
