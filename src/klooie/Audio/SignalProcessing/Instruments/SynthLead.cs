using System.Collections.Generic;

namespace klooie;

[SynthCategory("Lead")]
[SynthDescription("""
Wide, aggressive synth lead designed for heroic melodies. Combines detuned saw stacks,
harmonic square layers and octave-sub reinforcement with time-synced modulation,
chorus, delay and reverb for an epic, evolving tone.
""")]
public static class SynthLead
{
    public static ISynthPatch Create() => LayeredPatch.CreateBuilder()
         .AddLayer(volume: 0.8f, pan: -0.3f, transpose: 0, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Saw)
             .WithPickTransient(dur: 0.006f, gain: 0.55f)
             .WithEnvelope(0.005, 0.10, 0.90, 0.18)
             .WithDCBlocker()
             .WithVibrato(rateHz: 6.2f, depthCents: 22f)
             .WithPitchDrift(0.25f, 3f)
             .WithLowShelf(60f, -4f)
             .WithHighShelf(9000f, +3f)
             .WithAggroDistortion(7f, 0.8f, 0.015f)
             .WithChorus(delayMs: 17, depthMs: 6, rateHz: 0.25f, mix: 0.25f)
             .WithPingPongDelay(delayMs: 260, feedback: 0.42f, mix: 0.28f)
             .WithReverb(feedback: 0.75f, diffusion: 0.55f, wet: 0.35f, dry: 0.65f)
             .WithCompressor(0.55f, 3.5f, 0.008f, 0.032f)
             .WrapWithUnison(numVoices: 5, detuneCents: 12f, panSpread: 0.9f))
         .AddLayer(volume: 0.65f, pan: 0.3f, transpose: 12, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Square)
             .WithPickTransient(dur: 0.004f, gain: 0.45f)
             .WithEnvelope(0.003, 0.12, 0.85, 0.17)
             .WithSubOscillator(subOscLevel: 0.4f, subOscOctaveOffset: -1)
             .WithDCBlocker()
             .WithVibrato(rateHz: 5.6f, depthCents: 18f)
             .WithPitchDrift(0.2f, 2.5f)
             .WithPeakEQRelative(0.5f, +2.5f, 0.8f)
             .WithLowPassRelative(3.2f)
             .WithChorus(delayMs: 21, depthMs: 5, rateHz: 0.27f, mix: 0.22f)
             .WithPingPongDelay(delayMs: 260, feedback: 0.40f, mix: 0.26f)
             .WithReverb(feedback: 0.73f, diffusion: 0.53f, wet: 0.33f, dry: 0.67f)
             .WrapWithUnison(numVoices: 3, detuneCents: 8f, panSpread: 0.85f))
         .AddLayer(volume: 0.45f, pan: 0.0f, transpose: -12, patch: SynthPatch.Create()
             .WithWaveForm(WaveformType.Saw)
             .WithEnvelope(0.004, 0.11, 0.80, 0.20)
             .WithLowPassRelative(1.8f)
             .WithPresenceShelf(+2.5f)
             .WithReverb(feedback: 0.70f, diffusion: 0.50f, wet: 0.30f, dry: 0.70f)
             .WithCompressor(0.50f, 2.8f, 0.007f, 0.030f))
         .Build();
}
