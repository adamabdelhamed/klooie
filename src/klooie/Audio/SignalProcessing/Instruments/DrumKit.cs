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
            .WithPitchBend(KickPitchBend, KickDecay))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(0f, KickDecay, 0f, .01f)
            .WithLowPass(cutoffHz: 80)
            .WithVolume(.06f))
        .Build()
        .WithVolume(7f)
        .WithHighPass(400);
    
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
           .WithEnvelope(.01f, SnareDecay, 0f, .001f)
           .WithWaveForm(WaveformType.Sine)
           .WithMidiOverride(36)
           .WithPitchBend(SnarePitchBend, SnareDecay)
           .WithVolume(1))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.VioletNoise)
            .WithEnvelope(.001f, .1f, .1f, .01f)
            .WithReverb(feedback: .95f, diffusion: .55f, damping: .7f, wet: .5f, dry: .5f, duration: .1f)
            .WithVolume(.02f))
        .Build()
        .WithVolume(4f)
        .WithHighPass(400f);

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
        for(var i = 0; i < layers; i++)
        {
            builder.AddLayer(1, 0, 0, SynthPatch.Create()
            .WithWaveForm(WaveformType.PinkNoise)
            .WithEnvelope(currentAttack, currentDecay, 0.0f, 0.1f)
            .WithVolume(currentVolume)
            .WithChorus(delayMs: 22, depthMs: 15, rateHz: 4f, mix: .4f)
            .WithPeakEQ(freq: freqInc * i, gainDb: -8f, q: 1)
            .WithPeakEQ(freq: 500 + freqInc * i, gainDb: -2f, q: 1)
            .WithPeakEQ(freq: 2000 + freqInc * i, gainDb: 4f, q: 1)
            .WithLowPass(cutoffHz: 8000 - (i*100))
            .WithReverb(feedback: .2f, diffusion: .6f, damping: .2f,duration: .04f, wet: .15f, dry: .85f)
            .WithVolume(1));
            
            currentAttack = currentAttack + attackSpacing;
            currentDecay = currentDecay * decayDecay;
            currentVolume = currentVolume * volumeDecay;

        }

        var ret = builder.Build().WithVolume(.5f);
        return ret;
    }
}
