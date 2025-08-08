using System.Collections.Generic;

namespace klooie;

public static class DrumKit
{
    private const float KickDecay = .18f;

    [SynthCategory("Drums")]
    [SynthDocumentation("""
A basic kick drum patch with a punchy attack and a short decay.
""")]
    public static ISynthPatch Kick() => LayeredPatch.CreateBuilder()
    .AddLayer(patch: SynthPatch.Create()
        .WithWaveForm(WaveformType.Sine)
        .WithMidiOverride(36)
        .WithEnvelope(0f, KickDecay, 0f, 0.02f)
        .WithPitchBend(KickPitchBend, KickDecay)
        .WithSubOscillator(0.5f, -1)
        .WithLowShelf(50, 5f))
    .AddLayer(patch: SynthPatch.Create()
        .WithWaveForm(WaveformType.Noise)
        .WithEnvelope(0f, KickDecay * 1.5f, 0f, 0.01f)
        .WithLowPass(80)
        .WithVolume(0.06f))
    .Build()
    .WithReverb(0.8f, 0.6f, 0.3f, 0.25f, 0.75f, 0.3f)
    .WithVolume(3f)
    .WithHighPass(300);

    private static float KickPitchBend(float time)
    {
        float maxCents = 400f;
        if (time > KickDecay) return 0;
        float progress = time / KickDecay;
        return maxCents * (1f - progress) * (1f - progress);
    }

    private const float SnareDecay = .1f;

    [SynthCategory("Drums")]
    [SynthDocumentation("A basic snare drum.")]
    public static ISynthPatch Snare() => LayeredPatch.CreateBuilder()
    .AddLayer(patch: SynthPatch.Create()
        .WithWaveForm(WaveformType.Sine)
        .WithMidiOverride(38)
        .WithEnvelope(0.01f, SnareDecay, 0f, 0.01f)
        .WithPitchBend(SnarePitchBend, SnareDecay)
        .WithVolume(1.2f))
    .AddLayer(patch: SynthPatch.Create()
        .WithWaveForm(WaveformType.VioletNoise)
        .WithEnvelope(0.001f, 0.1f, 0.05f, 0.02f)
        .WithAggroDistortion(10, 0.7f, 0.1f)
        .WithChorus(12, 3, 0.4f, 0.3f)
        //.WithReverb(0.95f, 0.6f, 0.5f, 0.5f, 0.6f, 0.2f)
        .WithVolume(0.08f))
    .Build()
    .WithHighPass(600)
    .WithVolume(4f);

    private static float SnarePitchBend(float time)
    {
        float maxCents = 1200f;
        if (time > SnareDecay) return 0f;

        float progress = time / SnareDecay;
        // Cubic for a more pronounced drop at the end:
        float curve = 1f - (progress * progress);
        return maxCents * curve;
    }

    [SynthCategory("Drums")]
    [SynthDocumentation("A basic clap drum.")]
    public static ISynthPatch Clap()
    {
        var builder = LayeredPatch.CreateBuilder();
        var attackSpacing = .053f;
        var decayDecay = .55f;
        var volumeDecay = .4f;
        var layers = 8;

        var freqInc = 5f;

        var currentVolume = 1f;
        var currentAttack = .01f;
        var currentDecay = .08f;
        for (var i = 0; i < layers; i++)
        {
            builder.AddLayer(1, 0, 0, SynthPatch.Create()
            .WithWaveForm(WaveformType.PinkNoise)
            .WithEnvelope(currentAttack, currentDecay, 0.0f, 0.1f)
            .WithVolume(currentVolume)
            .WithPeakEQ(freq: freqInc * i, gainDb: -8f, q: 1)
            .WithPeakEQ(freq: 500 + freqInc * i, gainDb: -2f, q: 1)
            .WithPeakEQ(freq: 2000 + freqInc * i, gainDb: 4f, q: 1)
            .WithLowPass(cutoffHz: 8000 - (i * 100)));

            currentAttack = currentAttack + attackSpacing;
            currentDecay = currentDecay * decayDecay;
            currentVolume = currentVolume * volumeDecay;

        }

        var ret = builder.Build().WithVolume(.125f);
        return ret;
    }
}