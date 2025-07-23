using System.Collections.Generic;

namespace klooie;

[SynthCategory("Drums")]
[SynthDocumentation("""
A basic kick drum patch with a punchy attack and a short decay.
""")]
public static class Snare
{
    private const float Duration = .1f;
    public static ISynthPatch Create() => LayeredPatch.CreateBuilder()
        .AddLayer(patch: SynthPatch.Create()
            .WithEnvelope(.001f, Duration, 0f, .2f)
            .WithPeakEQRelative(multiplier: .5f, gainDb: -5f, q: 2f)
            .WithPeakEQRelative(multiplier: 1.5f, gainDb: -20f, q: 1f)
            .WithWaveForm(WaveformType.Sine)
            .WithPitchBend(KickPitchBend, .1f)
            .WithReverb(feedback: .1f,diffusion: .7f,wet: 1f, dry: 0f ))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(0f, Duration, 0f, .2f)
            .WithLowPass(cutoffHz: 100)
            .WithVolume(.5f)
            .WithReverb(feedback: .1f, diffusion: .7f, wet: 1f, dry: 0f))
        .Build()
        .WithVolume(3f);

    private static float KickPitchBend(float time)
    {
        float maxCents = 1200f;
        if (time > .1f) return 0;
        float progress = time / .1f;
        return maxCents * (1f - progress) * (1f - progress);
    }
}
