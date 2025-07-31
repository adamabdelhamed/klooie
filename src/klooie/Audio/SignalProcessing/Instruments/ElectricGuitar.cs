using System.Collections.Generic;

namespace klooie;

[SynthCategory("Lead")]
[SynthDocumentation("""
High‑gain guitar patch composed of multiple distortion stages, tone shaping
filters, cabinet simulation and ambience effects.  Ideal for aggressive rock
parts.
""")]
public static class ElectricGuitar
{
    public static ISynthPatch Create() => LayeredPatch.CreateBuilder()
        .AddLayer(volume: 0.6f, pan: -0.6f, transpose: 0, patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.PluckedString)
            .WithEnvelope(0.10f, 0.23f, 0.68f, 0.5f)
            .WithDCBlocker()
            .WithPeakEQRelative(.35f, +2.3f, .7f)
            .WithToneStack(bass: 2.1f, mid: 0.7f, treble: .60f)
            .WithPresenceShelf(-1.7f)
            .WithPeakEQRelative(.13f, -2f, 1.0f)
            .WithHighShelf(7000f, -4.5f)
            .WithAggroDistortion(10.5f, 0.74f, 0.025f)
            .WrapWithUnison(numVoices: 2, detuneCents: 8f, panSpread: 0.75f)
            .WrapWithPowerChord(intervals: [0, 7], detuneCents: 10f, panSpread: 1.15f))
        .AddLayer(volume: 0.48f, pan: +0.6f, transpose: 12, patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.PluckedString)
            .WithEnvelope(0.08f, 0.16f, 0.60f, 0.44f)
            .WithDCBlocker()
            .WithPeakEQRelative(.60f, +4f, .4f)
            .WithToneStack(bass: 1.1f, mid: 1.2f, treble: 1.3f)
            .WithPresenceShelf(0.6f)
            .WithPeakEQRelative(.25f, -3.8f, 0.93f)
            .WithHighShelf(8000f, -2f)
            .WithAggroDistortion(9f, 0.68f, 0.045f)
            .WrapWithUnison(numVoices: 2, detuneCents: 12f, panSpread: 0.87f))
        .AddLayer(volume: 0.38f, pan: 0.0f, transpose: 0, patch: SynthPatch.Create()
            .WithWaveForm(WaveformType.PluckedString)
            .WithEnvelope(0.2f, 0.11f, 0.62f, 0.7f)
            .WithDCBlocker()
            .WithPeakEQRelative(.52f, +3.1f, 0.56f)
            .WithToneStack(bass: 1.9f, mid: 1.8f, treble: 1.01f)
            .WithPresenceShelf(-0.7f)
            .WithAggroDistortion(7.5f, 0.79f, 0.022f))
        .Build();

}
