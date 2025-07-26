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
        //SoundProvider.Debug($"DEBUG: {DateTime.Now}".ToGreen());
        return LayeredPatch.CreateBuilder()
         .AddLayer(volume: 0.8f, pan: -0.3f, transpose: 0, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Saw)
             .WithEnvelope(0.005, 0.10, 0.90, 0.18)
             .WithDCBlocker()
             .WithPitchDrift(0.25f, 3f)
             .WrapWithUnison(numVoices: 5, detuneCents: 12f, panSpread: 0.9f))
         .AddLayer(volume: 0.65f, pan: 0.3f, transpose: 12, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Square)
             .WithEnvelope(0.003, 0.12, 0.85, 0.17)
             .WithSubOscillator(subOscLevel: 0.4f, subOscOctaveOffset: -1)
             .WithDCBlocker()
             .WithPitchDrift(0.2f, 2.5f)
             .WithPeakEQRelative(0.5f, +2.5f, 0.8f)
             .WithLowPassRelative(3.2f)
             .WrapWithUnison(numVoices: 3, detuneCents: 8f, panSpread: 0.85f))
         .AddLayer(volume: 0.45f, pan: 0.0f, transpose: -12, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Saw)
             .WithEnvelope(0.004, 0.11, 0.80, 0.20))
         .Build()
         .WithPortamento()
         .WithVolume(1);
    }
}
