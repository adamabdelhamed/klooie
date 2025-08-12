using System.Collections.Generic;

namespace klooie;

[SynthCategory("Lead")]
[SynthDocumentation("""
Development in progress
""")]
public static class ElectricGuitar
{
    public static ISynthPatch Create(NoteExpression note) => 
        LayeredPatch.CreateBuilder()
            .AddLayer
            (
                volume: 0.6f, pan: 0f, transpose: 0, patch: SynthPatch.Create(note)
                .WithWaveForm(WaveformType.PluckedString)
                .WithEnvelope(delay: 0, attack: 0f, decay: 0.23f, sustainLevel: 0.7f, release: 0.5f)
                .WithToneStack(bass: 4.1f, mid: 0.7f, treble: .60f)
                .WithLFO(SynthSignalSource.LfoTarget.VibratoDepth, rateHz: 6, depth: 8, shape: 0, phaseOffset: 0f, velocityAffectsDepth: false)
                .WithPresenceShelf(presenceDb: 1.7f)
                .WithAggroDistortion(drive: 10f, stageRatio: 0.74f, bias: 0.025f)
                .WrapWithUnison(numVoices: 3, detuneCents: 8f, panSpread: 1f)
                .WrapWithPowerChord(intervals: [0, 7, 12], detuneCents: 10f, panSpread: 1f)
                .WithReverb(feedback: .7f, diffusion: .75f, wet: .4f, dry: .5f, damping: .45f, duration: .5f, inputLowpassHz: 9500f, velocityAffectsMix: true, mixVelocityCurve: EffectContext.EaseInOutCubic, enableModulation: true)
            )
        .Build()
        .WithVolume(.08f);
}
