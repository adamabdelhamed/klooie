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

            // --- PRE-DISTORTION EQ ---
            .WithLowShelf(120f, -5f)               // Cut sub-bass mud below 120Hz
            .WithPeakEQ(800f, +3f, 0.6f)           // Add bite/body around 800Hz
            .WithHighPass(110f)

            // 1️⃣ Pick + micro-fade
            .WithPickTransient(.0025f, .35f)
            .WithFadeIn(0.005f)

            // 2️⃣ Pre-gate
            .WithNoiseGate(openThresh: 0.02f,
                           closeThresh: 0.018f,
                           attackMs: 4f,
                           releaseMs: 45f)

            // --- DISTORTION & STACK ---
            .WithVolume(4.2f)
            .WithAggroDistortion(18f, 0.8f, 0.1f)
            .WithToneStack(1.10f, 0.75f, 1.55f)
            .WithCabinet()
            .WithPresenceShelf(-3f)
            .WithLowPass(0.019f)

            // --- POST-CAB EQ ---
            .WithPeakEQ(400f, -3f, 1.0f)           // Remove boxiness at 400Hz
            .WithHighShelf(6000f, -4f)             // Tame fizz above 6kHz

            // --- COMPRESS/ENVELOPE ---
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

        // Unison for width
        var wide = UnisonPatch.Create(
            numVoices: 2,
            detuneCents: 8f,
            panSpread: 0.9f,
            basePatch: core);

        // PowerChord for root + 5th
        var powerChord = PowerChordPatch.Create(
            basePatch: wide,
            intervals: new int[] { 0, 7 },
            detuneCents: 6f,
            panSpread: 1.1f
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
