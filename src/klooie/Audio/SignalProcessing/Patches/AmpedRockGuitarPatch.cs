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
        .WithLowShelf(.2f, -5f)
        .WithPeakEQRelative(.5f, +3f, 0.6f)
        .WithHighPassRelative(1.2f)
        .WithNoiseGate(openThresh: 0.02f, closeThresh: 0.018f, attackMs: 4f,  releaseMs: 5f)
        .WithAggroDistortion(13f, 0.8f, 0.04f)
        .WithToneStack(2.10f, 1.25f, .55f)
        .WithCabinet()
        .WithPresenceShelf(-2f)
        .WithLowPassRelative(2f)
        .WithPeakEQRelative(.2f, -3f, 1.0f)
        .WithHighShelf(6000f, -4f)
        .WithReverb(feedback: 0.3f, diffusion: 0.28f, wet: 0.2f, dry: 0.75f)
        .WithCompressor(.45f, 5f, 0.01f, 0.050f)
        .WithNoiseGate(openThresh: 0.04f, closeThresh: 0.036f, attackMs: 2f, releaseMs: 35f)
        .WithEnvelope(0.07f, 0.2, 0.70, 0.5)
        .WrapWithUnison(numVoices: 2, detuneCents: 10f, panSpread: 0.9f)
        .WrapWithPowerChord([0, 7, 12], 10f, 1.1f);
}
