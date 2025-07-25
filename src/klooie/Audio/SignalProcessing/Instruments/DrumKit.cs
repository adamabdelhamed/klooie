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
            .WithEnvelope(0f, KickDecay, 0f, 0.02f)
            .WithPeakEQRelative(multiplier: .75f, gainDb: 2f, q: 3f)
            .WithPeakEQRelative(multiplier: 1.25f, gainDb: -2f, q: 3f)
            .WithPitchBend(KickPitchBend, KickDecay))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(0f, KickDecay, 0f, .01f)
            .WithLowPass(cutoffHz: 80)
            .WithVolume(.03f))
        .Build()
        .WithVolume(1f);
    
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
           .WithPitchBend(SnarePitchBend, SnareDecay)
           .WithVolume(1))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(.001f, .1f, .1f, .01f)
            .WithReverb(feedback: .8f, diffusion: .55f, damping: .5f, wet: .5f, dry: .5f, duration: .05f)
            .WithVolume(.05f))
        .Build()
        .WithVolume(1f);

    private static float SnarePitchBend(float time)
    {
        float maxCents = 1200f;
        if (time > SnareDecay) return 0f;

        float progress = time / SnareDecay;
        // Cubic for a more pronounced drop at the end:
        float curve = 1f - (progress * progress);
        return maxCents * curve;
    }
}
