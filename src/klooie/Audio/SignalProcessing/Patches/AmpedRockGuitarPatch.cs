using System.Collections.Generic;

namespace klooie;

/// <summary>
/// Modern high-gain guitar “amp-stack”:
///   pre-HPF → pre-gain → AggroDistortion → Tone Stack → 4×12 cab →
///   LPF fizz-tamer → compressor → width / ambience.
/// </summary>
public sealed class AmpedRockGuitarPatch : Recyclable, ISynthPatch, ICompositePatch
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
            .WithVibrato(rateHz: 6f, depthCents: 28f)
            .WithLowShelf(120f, -5f)
            .WithPeakEQ(800f, +3f, 0.6f)
            .WithHighPass(110f)
            .WithPickTransient(.0025f, .35f)
            .WithFadeIn(0.005f)
            .WithNoiseGate(openThresh: 0.02f,
                           closeThresh: 0.018f,
                           attackMs: 4f,
                           releaseMs: 45f)
            .WithVolume(4.2f)
            .WithAggroDistortion(18f, 0.8f, 0.1f)
            .WithToneStack(1.10f, 0.75f, 1.55f)
            .WithCabinet()
            .WithPresenceShelf(-3f)
            .WithLowPass(136f)
            .WithPeakEQ(400f, -3f, 1.0f)
            .WithHighShelf(6000f, -4f)

            // ----- ADD YOUR PING-PONG DELAY HERE -----
            .WithPingPongDelay(delayMs: 340f, feedback: 0.47f, mix: 0.22f)

            // ----- REVERB IMMEDIATELY AFTER DELAY -----
            .WithReverb(feedback: 0.65f, diffusion: 0.38f, wet: 0.16f, dry: 0.75f)

            // Final stage: compression/gate/envelope
            .WithEffect(CompressorEffect.Create(new CompressorEffect.Settings() {
                Threshold= 0.55f,
                Ratio= 6f,
                Attack = 0.003f,
                Release= 0.010f}))
            .WithNoiseGate(openThresh: 0.04f, closeThresh: 0.036f,
                           attackMs: 2f, releaseMs: 35f)
            .WithEffect(EnvelopeEffect.Create(
                attack: 0.01,
                decay: 0.12,
                sustain: 0.60,
                release: 0.22));

        var wide = UnisonPatch.Create(
            numVoices: 2,
            detuneCents: 8f,
            panSpread: 0.9f,
            basePatch: core);

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

    public bool EnableVibrato => inner.EnableVibrato;
    public float VibratoRateHz => inner.VibratoRateHz;
    public float VibratoDepthCents => inner.VibratoDepthCents;
    public float VibratoPhaseOffset => inner.VibratoPhaseOffset;

    public void SpawnVoices(float freq,
                            VolumeKnob master,
                            NoteExpression note,
                            List<SynthSignalSource> outVoices)
        => inner.SpawnVoices(freq, master, note, outVoices);

    
    public void GetPatches(List<ISynthPatch> patches)
    {
        patches.Add(inner);
    }

    protected override void OnReturn()
    {
        if (inner is Recyclable r) r.TryDispose();
        inner = null!;
        base.OnReturn();
    }
}
