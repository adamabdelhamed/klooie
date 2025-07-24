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
            .WithReverb(feedback: .7f, diffusion: .4f, damping: 0f, wet: 0.02f, dry: .8f)
           .WithPitchBend(KickPitchBend, Duration))
        .AddLayer(patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithEnvelope(.001f, .1f, 0f, .001f)
         //   .WithLowPass(4000)
          //  .WithVolume(.2f)
            .WithReverb(feedback: .7f, diffusion: .4f, damping: 0f, wet: 0.02f, dry: .8f)
            .WithVolume(.3f))
        .Build()
       // .WithCompressor(threshold: .5f, ratio: 4f, attackMs:.1f, releaseMs: .05f)
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
