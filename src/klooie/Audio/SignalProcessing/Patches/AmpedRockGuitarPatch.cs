using System.Collections.Generic;

namespace klooie;

/// <summary>
/// Modern high-gain guitar “amp-stack”:
///   pre-HPF → pre-gain → AggroDistortion → Tone Stack → 4×12 cab →
///   LPF fizz-tamer → compressor → width / ambience.
/// </summary>
public sealed class AmpedRockGuitarPatch : Recyclable, ISynthPatch
{
    /* -------------------------------------------------------------------- */
    private ISynthPatch inner;
    public ISynthPatch InnerPatch => inner;
    private static readonly LazyPool<AmpedRockGuitarPatch> _pool =
        new(() => new AmpedRockGuitarPatch());
    private AmpedRockGuitarPatch() { }

    public static AmpedRockGuitarPatch Create()
    {
        var p = _pool.Value.Rent();
        p.inner = BuildInner();
        return p;
    }

    private static ISynthPatch BuildInner()
    {
        var core = SynthPatch.Create()
            .WithWaveForm(WaveformType.PluckedString)
            .WithDCBlocker()
            .WithPitchDrift(0.25f, 2f)
            .WithHighPass(110f)

            /* 1️⃣  Pick + micro-fade come first ------------------------------ */
            .WithPickTransient(.0025f, .35f)
            .WithFadeIn(0.005f)

            /* 2️⃣  NOW the pre-gate, with slower attack ---------------------- */
            .WithNoiseGate(openThresh: 0.02f,
                           closeThresh: 0.018f,
                           attackMs: 4f,
                           releaseMs: 45f)

            /* --- rest of the chain unchanged ------------------------------- */
            .WithVolume(4.2f)
            .WithAggroDistortion(18f, 0.8f, 0.1f)
            .WithToneStack(1.10f, 0.75f, 1.55f)
            .WithCabinet()
            .WithPresenceShelf(-3f)
            .WithLowPass(0.019f)
            .WithEffect(CompressorEffect.Create(
                threshold: 0.55f,
                ratio: 6f,
                attack: 0.003f,
                release: 0.010f))
            .WithNoiseGate(openThresh: 0.04f, closeThresh: 0.036f,
                           attackMs: 2f, releaseMs: 35f)
            .WithEffect(EnvelopeEffect.Create(
                attack: 0.01,
                decay: 0.12,
                sustain: 0.60,
                release: 0.22));

        // First layer: Unison for stereo width
        var wide = UnisonPatch.Create(
            numVoices: 2,
            detuneCents: 8f,
            panSpread: 0.9f,
            basePatch: core);

        // Second layer: PowerChordPatch to stack root+5th (+octave, if you like)
        var powerChord = PowerChordPatch.Create(
            basePatch: wide,
            intervals: new int[] { 0, 7 },    // root + 5th; add 12 for root+5th+octave
            detuneCents: 6f,                  // subtle extra thickness per interval
            panSpread: 1.1f                   // wide stereo spread for chord
        );

        return powerChord;
    }



    /* ISynthPatch proxy ---------------------------------------------------- */
    public WaveformType Waveform => inner.Waveform;
    public float DriftFrequencyHz => inner.DriftFrequencyHz;
    public float DriftAmountCents => inner.DriftAmountCents;
    public bool EnablePitchDrift => inner.EnablePitchDrift;
    public bool EnableSubOsc => inner.EnableSubOsc;
    public int SubOscOctaveOffset => inner.SubOscOctaveOffset;
    public float SubOscLevel => inner.SubOscLevel;
    public bool EnableTransient => inner.EnableTransient;
    public float TransientDurationSeconds => inner.TransientDurationSeconds;
    public int Velocity => inner.Velocity;
    public RecyclableList<IEffect> Effects => inner.Effects;

    public void SpawnVoices(float freq,
                            VolumeKnob master,
                            VolumeKnob? sampleKnob,
                            List<SynthSignalSource> outVoices)
        => inner.SpawnVoices(freq, master, sampleKnob, outVoices);

    protected override void OnReturn()
    {
        if (inner is Recyclable r) r.TryDispose();
        inner = null!;
        base.OnReturn();
    }
}
