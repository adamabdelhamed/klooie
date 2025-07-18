using System.Collections.Generic;

namespace klooie;

[SynthCategory("Guitar")]
[SynthDescription("""
Moderate-gain rock guitar patch featuring layered unison voices and
power-chord assembly for realistic riffs.
""")]
public sealed class RockGuitar2 : Recyclable, ISynthPatch, ICompositePatch
{
    public bool IsNotePlayable(int midiNote) => midiNote >= 37 && midiNote <= 60;
    private ISynthPatch inner;
    public ISynthPatch InnerPatch => inner;
    private static readonly LazyPool<RockGuitar2> _pool =
        new(() => new RockGuitar2());
    private RockGuitar2() { }

    public static RockGuitar2 Create()
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
            .WithPitchDrift(0.12f, 1.2f)
            .WithVibrato(rateHz: 5.7f, depthCents: 18f)
            .WithLowShelf(120f, -2.5f)
            .WithPeakEQ(850f, +3.5f, 0.8f)
            .WithPeakEQ(400f, -3.5f, 0.9f)
            .WithPeakEQ(2300f, +1.5f, 1.0f)   // less treble boost
            .WithHighPass(110f)
            .WithPickTransient(.0018f, .38f)
            .WithFadeIn(0.004f)
            .WithNoiseGate(openThresh: 0.017f,
                           closeThresh: 0.015f,
                           attackMs: 2.0f,
                           releaseMs: 31f)
            .WithVolume(3.5f)                         // less gain, less fizz
            .WithAggroDistortion(7f, 0.75f, 0.12f)   // much less drive
            .WithToneStack(1.08f, 0.75f, 1.2f)        // modest scoop/bright
            .WithCabinet()
            .WithPresenceShelf(+1.2f)
            .WithLowPass(158f)                      // higher cutoff, more highs get through
            .WithPingPongDelay(delayMs: 170f, feedback: 0.24f, mix: 0.11f)
            .WithReverb(feedback: 0.38f, diffusion: 0.21f, wet: 0.08f, dry: 0.87f)
            .WithEffect(CompressorEffect.Create(new CompressorEffect.Settings() { Threshold = .56f
            , Ratio = 6.5f, Attack = .0018f, Release = .011f}))
            .WithNoiseGate(openThresh: 0.018f, closeThresh: 0.014f,
                           attackMs: 1.2f, releaseMs: 18f)
            .WithEffect(EnvelopeEffect.Create(
                attack: 0.004f,
                decay: 0.09f,
                sustain: 0.77f,
                release: 0.14f));

        // 2-voice unison is safer; for >64 MIDI, reduce to 1 voice
        var wide = UnisonPatch.Create(
            numVoices: 2,
            detuneCents: 7.5f,
            panSpread: 0.85f,
            basePatch: core);

        // Single note only, for “realism”
        var powerChord = PowerChordPatch.Create(
            basePatch: wide,
            intervals: new int[] { 0 },
            detuneCents: 4.5f,
            panSpread: 0.7f
        );

        return powerChord;
    }


    // Proxy properties
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

    public void GetPatches(List<ISynthPatch> patches)
    {
        patches.Add(inner);
    }

    public void SpawnVoices(float freq,
                            VolumeKnob master,
                            NoteExpression note,
                            List<SynthSignalSource> outVoices)
        => inner.SpawnVoices(freq, master, note, outVoices);

    protected override void OnReturn()
    {
        if (inner is Recyclable r) r.TryDispose();
        inner = null!;
        base.OnReturn();
    }
}
