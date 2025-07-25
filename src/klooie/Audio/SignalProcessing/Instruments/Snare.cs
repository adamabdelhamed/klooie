using System.Collections.Generic;

namespace klooie;

[SynthCategory("Drums")]
[SynthDocumentation("""

""")]
public static class Snare
{
    private const float Duration = .1f;
    public static ISynthPatch Create() => LayeredPatch.CreateBuilder()
        .AddLayer(patch: SynthPatch.Create()
           .WithEnvelope(.01f, Duration, 0f, .001f)
           .WithWaveForm(WaveformType.Sine)
           .WithPitchBend(KickPitchBend, Duration)
           .WithVolume(1))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(.001f, .1f, .1f, .01f)
            .WithReverb(feedback: .8f, diffusion: .55f, damping: .5f, wet: .5f, dry: .5f, duration: .05f)
            .WithVolume(.05f))
        .Build()
        .WithVolume(1f);

    private static float KickPitchBend(float time)
    {
        float maxCents = 1200f;
        if (time > Duration) return 0f;

        float progress = time / Duration;
        // Cubic for a more pronounced drop at the end:
        float curve = 1f - (progress * progress);
        return maxCents * curve;
    }
}
