using System.Collections.Generic;

namespace klooie;

[SynthCategory("Guitar")]
[SynthDescription("""
High‑gain guitar patch composed of multiple distortion stages, tone shaping
filters, cabinet simulation and ambience effects.  Ideal for aggressive rock
parts.
""")]
public static class AmpedRockGuitarPatch
{
    public static ISynthPatch Create() => SynthPatch.Create()
        .WithWaveForm(WaveformType.PluckedString)
        .WithPickTransient(dur: 0.01f, gain: 0.7f)
        .WithDCBlocker()
        .WithVibrato(rateHz: 6f, depthCents: 30f)
        .WithLowShelf(freq: 20f,gainDb: -5f)
        .WithPeakEQRelative(multiplier: .5f, gainDb: +3f,q: 0.6f)
        .WithHighPassRelative(multiplier: 1.2f)
        .WithNoiseGate(openThresh: 0.02f, closeThresh: 0.018f, attackMs: 4f,  releaseMs: 5f)
        .WithAggroDistortion(drive: 13f, stageRatio: 0.8f, bias: 0.04f)
        .WithToneStack(bass: 2.10f, mid: 1.25f, treble: .55f)
        .WithCabinet()
        .WithPresenceShelf(presenceDb: -2f)
        .WithLowPassRelative(multiplier: 2f)
        .WithPeakEQRelative(multiplier: .2f,gainDb: -3f, q: 1.0f)
        .WithHighShelf(freq: 6000f, gainDb: -4f)
        .WithReverb(feedback: 0.3f, diffusion: 0.28f, wet: 0.2f, dry: 0.75f)
        .WithCompressor(threshold: .45f,ratio: 5f,attackMs: 0.01f,releaseMs: 0.050f)
        .WithNoiseGate(openThresh: 0.04f, closeThresh: 0.036f, attackMs: 2f, releaseMs: 35f)
        .WithEnvelope(attackMs: 0.07f,decayMs: 0.2,sustainLevel: 0.70,releaseMs: 0.5)
        .WrapWithUnison(numVoices: 2, detuneCents: 10f, panSpread: 0.9f)
        .WrapWithPowerChord(intervals: [0, 7, 12], detuneCents: 10f, panSpread: 1.1f);
}
