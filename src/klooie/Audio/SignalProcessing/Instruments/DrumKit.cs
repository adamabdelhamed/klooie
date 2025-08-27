using System.Collections.Generic;

namespace klooie;

public static class DrumKit
{
    private const float KickDecay = .18f;

    [SynthCategory("Drums")]
    [SynthDocumentation("""
A basic kick drum patch with a punchy attack and a short decay.
""")]
    public static ISynthPatch Kick(NoteExpression note) => LayeredPatch.CreateBuilder()
    .AddLayer(patch: SynthPatch.Create(note)
        .WithWaveForm(WaveformType.Sine)
        .WithMidiOverride(36)
        .WithEnvelope(0f, KickDecay, 0f, 0.02f)
        .WithPitchBend(KickPitchBend, KickDecay)
        .WithSubOscillator(0.5f, -1)
        .WithLowShelf(50, 5f))
    .AddLayer(patch: SynthPatch.Create(note)
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
    public static ISynthPatch Snare(NoteExpression note) => LayeredPatch.CreateBuilder()
    .AddLayer(patch: SynthPatch.Create(note)
        .WithWaveForm(WaveformType.Sine)
        .WithMidiOverride(38)
        .WithEnvelope(0.01f, SnareDecay, 0f, 0.01f)
        .WithPitchBend(SnarePitchBend, SnareDecay)
        .WithVolume(1.2f))
    .AddLayer(patch: SynthPatch.Create(note)
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
    public static ISynthPatch Clap(NoteExpression note)
    {
        var builder = LayeredPatch.CreateBuilder();

        var delaySpacing = 0.01f;   // layer-to-layer offset (sec)
        var attack = 0.02f; 
        var decay = 0.09f; 
        var layers = 3;

        var volume = 1f;
        for (var i = 0; i < layers; i++)
        {
            builder.AddLayer(1, 0, 0, SynthPatch.Create(note)
                .WithWaveForm(WaveformType.PinkNoise)
                .WithEnvelope(
                    delay: i * delaySpacing, 
                    attack: t=> ADSRCurves.Cubic(t, attack), 
                    decay: t => ADSRCurves.Cubic(t, decay), 
                    sustainLevel: t => 0,
                    release: t => null)
                .WithVolume(volume)
                );
            volume *= 0.3f;
        }

        return builder.Build().WithVolume(.08f);
    }
}