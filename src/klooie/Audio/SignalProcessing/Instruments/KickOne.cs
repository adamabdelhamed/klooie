using System.Collections.Generic;

namespace klooie;

[SynthCategory("Drums")]
[SynthDocumentation("""
A basic kick drum patch with a punchy attack and a short decay.
""")]
public static class KickOne
{
    private const float Duration = .2f;
    public static ISynthPatch Create() => LayeredPatch.CreateBuilder()
        .AddLayer(patch: SynthPatch.Create()
            .WithEnvelope(0f, Duration, 0f, .01f)
            .WithPeakEQRelative(multiplier: .5f, gainDb: 10f, q: 2f)
            .WithPeakEQRelative(multiplier: 1.5f, gainDb: -20f, q: 1f)
            .WithWaveForm(WaveformType.Sine)
            .WithPitchBend(KickPitchBend, Duration))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(0f, Duration, 0f, .01f)
            .WithLowPass(cutoffHz: 40)
            .WithVolume(.04f))
        .Build()
        .WithVolume(3f);

    private static float KickPitchBend(float time)
    {
        float maxCents = 50f;
        if (time > Duration) return 0;
        float progress = time / Duration;
        return maxCents * (1f - progress) * (1f - progress);
    }
}
