using System.Collections.Generic;

namespace klooie;

[SynthCategory("Drums")]
[SynthDocumentation("""
A basic kick drum patch with a punchy attack and a short decay.
""")]
public static class KickOne
{
    private const float Duration = .18f;
    public static ISynthPatch Create() => LayeredPatch.CreateBuilder()
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithEnvelope(0f, Duration, 0f, 0.02f)
            .WithPeakEQRelative(multiplier: .75f, gainDb: 2f, q: 3f)
            .WithPeakEQRelative(multiplier: 1.25f, gainDb: -2f, q: 3f)
            .WithPitchBend(KickPitchBend, Duration))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(0f, Duration, 0f, .01f)
            .WithLowPass(cutoffHz: 80)
            .WithVolume(.03f))
        .Build()
        .WithVolume(1f);

    private static float KickPitchBend(float time)
    {
        float maxCents = 400f;
        if (time > Duration) return 0;
        float progress = time / Duration;
        return maxCents * (1f - progress) * (1f - progress);
    }
}
