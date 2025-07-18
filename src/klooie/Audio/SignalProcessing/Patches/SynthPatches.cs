using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class SynthPatches
{
    public static ISynthPatch CreateGuitar()
    => SynthPatch.Create()
        .WithLowPass(143f)
        .WithEnvelope(.005f, 0.1f, 0.25f, 0.4f);


    public static ISynthPatch CreateBass()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Sine)
        .WithSubOscillator(.06f, - 1)
        .WithLowPass(71f)
        .WithDistortion()
        .WithEnvelope(0.005f, 0.12f, 0.5f, 0.4f);

    public static ISynthPatch CreateAnalogPad()
     => SynthPatch.Create()
         .WithWaveForm(WaveformType.Saw)
         .WithPitchDrift(0.3f, 7f)
         .WithSubOscillator(0.5f, -1)
         .WithChorus(delayMs: 22, depthMs: 7, rateHz: 0.22f, mix: 0.19f)
         .WithReverb(feedback: 0.73f, diffusion: 0.52f, wet: 0.26f, dry: 0.74f)
         .WithVolume(.1f)
         .WithEffect(EnvelopeEffect.Create(0.23f, 1.3f, 0.85f, 1.6f));

    public static ISynthPatch CreateLead()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Saw)
        .WithPitchDrift(0.2f, 3f)
        .WithDistortion()
        .WithLowPass(217f)
        .WithChorus()
        .WithReverb()
        .WithEffect(EnvelopeEffect.Create(0.01f, 0.15f, 0.6f, 0.25f));

    public static ISynthPatch CreateKick()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Sine)
        .WithTransient(0.005f)
        .WithDistortion()
        .WithHighPass(20f)
        .WithEffect(EnvelopeEffect.Create(0, 0.1f, 0.0f, 0.1f));

    public static ISynthPatch CreateSnare()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Noise)
        .WithTransient(0.005f)
        .WithDistortion()
        .WithLowPass(1755f)
        .WithHighPass(1000f)
        .WithReverb()
        .WithEffect(EnvelopeEffect.Create(0, .1, 0, 0.2f));

    public static ISynthPatch CreateRhythmicPad()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Saw)
        .WithPitchDrift(0.15f, 5f)
        .WithSubOscillator(0.5f, -1)
        .WithTremolo(0.5f, 5f)
        .WithLowPass(107f)
        .WithReverb()
        .WithEffect(EnvelopeEffect.Create(0.5f, 1.0f, 0.75f, 1.5f));

    public static ISynthPatch CreateRockGuitar()
            => AmpedRockGuitarPatch.Create();

    public static ISynthPatch CreateRockGuitar2()
        => RockGuitar2.Create();

    public static ISynthPatch CreateDeepSubBass()
      => SynthPatch.Create()
          .WithWaveForm(WaveformType.Sine)
          .WithSubOscillator(subOscLevel: .18f, subOscOctaveOffset: -1)
          .WithLowPass(42f)                      // extra-dark
          .WithDCBlocker()
          .WithVolume(.9f)
          .WithEnvelope(0.004, 0.12, 0.6, 0.28);          // fast/tight

    /// <summary>Clean, almost FM-like bass with a touch of bite up top.</summary>
    public static ISynthPatch CreateCleanDigitalBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithHighPass(220f)                             // remove mud
            .WithLowPass(85f)
            .WithNoiseGate(.03f, .025f)                     // surgical silence
            .WithVolume(.8f)
            .WithEnvelope(0.002, 0.08, 0.8, 0.12);

    /// <summary>Short, percussive pluck for arpeggiated basslines.</summary>
    public static ISynthPatch CreateTightPluckBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithTransient(.006f)
            .WithLowPass(143f)
            .WithNoiseGate(.04f, .03f, attackMs: 1f, releaseMs: 40f)
            .WithVolume(.7f)
            .WithEnvelope(0.0015, 0.045, 0.35, 0.11);

    /// <summary>Stereo-chorused pad bass that fills out the mid-low range.</summary>
    public static ISynthPatch CreateChorusPadBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithSubOscillator(.12f, -1)
            .WithChorus(delayMs: 18, depthMs: 9, rateHz: .23f, mix: .25f)
            .WithLowPass(107f)
            .WithVolume(.75f)
            .WithEnvelope(0.01, 0.22, 0.7, 0.55);

    /// <summary>Aggressive, distorted growl suited for modern EDM drops.</summary>
    public static ISynthPatch CreateAggroDistBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithAggroDistortion(drive: 10f, stageRatio: .7f, bias: .12f)
            .WithLowPass(129f)
            .WithVolume(.8f)
            .WithEnvelope(0.003, 0.11, 0.5, 0.27);

    /// <summary>Spacious bass with long tail—great for cinematic moments.</summary>
    public static ISynthPatch CreateReverbBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithSubOscillator(.1f, -1)
            .WithReverb(feedback: .82f, diffusion: .55f, wet: .28f, dry: .72f)
            .WithLowPass(85f)
            .WithVolume(.8f)
            .WithEnvelope(0.006, 0.18, 0.6, 1.2);           // long release

    /// <summary>Pulsing dub-style wobble—use tremolo rate to lock to tempo.</summary>
    public static ISynthPatch CreateDubPulseBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithSubOscillator(.14f, -1)
            .WithTremolo(depth: .55f, rateHz: 2f)           // 120 BPM ⇒ ¼-note wobble
            .WithLowPass(100f)
            .WithVolume(.85f)
            .WithEnvelope(0.004, 0.1, 0.65, 0.3);

    /// <summary>Noisy, gnarly bass with a hint of pitch drift for movement.</summary>
    public static ISynthPatch CreateNoisyGrowlBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithPitchDrift(.6f, 12f)                       // subtle detune
            .WithAggroDistortion(drive: 8f)
            .WithLowPass(143f)
            .WithHighPass(90f)
            .WithVolume(.85f)
            .WithEnvelope(0.003, 0.09, 0.55, 0.25);

    public static ISynthPatch CreateFatLayeredBass()
    {
        // --- Main "body" layer: square, pick transient, drive ---
        var main = SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithPickTransient(.003f, .45f)
            .WithLowPass(100f)
            .WithAggroDistortion(5f, 0.7f, 0.08f)
            .WithNoiseGate(.02f, .018f)
            .WithVolume(.70f)
            .WithEnvelope(0.002, 0.045, 0.45, 0.13);

        // --- Sub layer: pure sine, dark, just for weight ---
        var sub = SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithLowPass(57f)
            .WithVolume(.38f)
            .WithEnvelope(0.002, 0.05, 0.8, 0.16);

        // --- Click/attack layer: filtered noise, fast envelope ---
        var click = SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithHighPass(900f)
            .WithLowPass(957f)
            .WithPickTransient(.002f, .75f)
            .WithVolume(.15f)
            .WithEnvelope(0.0007, 0.009, 0.05, 0.02);

        var rootPlusSub = PowerChordPatch.Create(
            new PowerChordPatch.Settings
            {
                BasePatch = main,
                Intervals = new[] { 0, -12 },      // root + sub-octave
                DetuneCents = 0f,
                PanSpread = 0f
            }
        );

        var fatWide = UnisonPatch.Create(
            new UnisonPatch.Settings
            {
                BasePatch = rootPlusSub,
                NumVoices = 2,
                DetuneCents = 3.5f,
                PanSpread = 0.18f
            }
        );

        // --- Blend main/sub (fatWide) + click using LayeredPatch ---
        // (If you want to add 'sub' as a separate layer, just add it here too)
        return LayeredPatch.Create(
            patches: new ISynthPatch[] { fatWide, click },
            volumes: new float[] { 1.0f, 0.9f }, // adjust to taste
            pans: new float[] { 0.0f, 0.0f }  // both centered; tweak for stereo
        );
    }



    /// <summary>Click-accented tech bass—fast transient, very short release.</summary>
    public static ISynthPatch CreateTechClickBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithPickTransient(.004f, .7f)
            .WithLowPass(78f)
            .WithNoiseGate(.03f, .028f)
            .WithVolume(.75f)
            .WithEnvelope(0.001, 0.03, 0.4, 0.07);

    /// <summary>Swept, filtered slide perfect for fills or transitions.</summary>
    public static ISynthPatch CreateFilteredSlideBass()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithSubOscillator(.08f, -1)
            .WithLowPass(143f)
            .WithHighPass(110f)
            .WithTremolo(depth: .4f, rateHz: 0.35f)         // slow sweep
            .WithVolume(.8f)
            .WithEnvelope(0.005, 0.15, 0.55, 0.5);

    public static ISynthPatch CreateBrightSawLead()
       => SynthPatch.Create()
           .WithWaveForm(WaveformType.Saw)
           .WithChorus(delayMs: 15, depthMs: 6, rateHz: .28f, mix: .33f)
           .WithHighPass(300f)
           .WithPresenceShelf(+4f)
           .WithVolume(.8f)
           .WithEnvelope(0.002, 0.06, 0.8, 0.18);

    /// <summary>Glass-y, bell-like keys—great for arpeggios.</summary>
    public static ISynthPatch CreateCrystalBell()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithLowPass(64f)                             // keeps it sparkly
            .WithHighPass(350f)
            .WithReverb(feedback: .75f, diffusion: .6f, wet: .38f, dry: .7f)
            .WithVolume(.7f)
            .WithEnvelope(0.003, 0.12, 0.7, 0.95);          // lingering tail

    /// <summary>Fast, percussive pluck for staccato melodies.</summary>
    public static ISynthPatch CreateHyperPluckLead()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithTransient(.004f)
            .WithHighPass(400f)
            .WithLowPass(107f)
            .WithVolume(.75f)
            .WithEnvelope(0.001, 0.04, 0.35, 0.07);

    /// <summary>Dreamy airy pad that floats above the mix.</summary>
    public static ISynthPatch CreateAirPadLead()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithPitchDrift(.4f, 9f)
            .WithSubOscillator(.08f, -1)                    // just a hint
            .WithChorus(delayMs: 20, depthMs: 10, rateHz: .18f, mix: .4f)
            .WithReverb(feedback: .82f, diffusion: .6f, wet: .32f, dry: .68f)
            .WithHighPass(250f)
            .WithLowPass(129f)
            .WithVolume(.72f)
            .WithEnvelope(0.01, 0.3, 0.8, 0.9);

    /// <summary>8-bit style square-wave lead—super clean and direct.</summary>
    public static ISynthPatch CreateChiptuneLead()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithHighPass(450f)
            .WithNoiseGate(.03f, .028f)
            .WithVolume(.85f)
            .WithEnvelope(0.0008, 0.03, 0.9, 0.06);

    /// <summary>FM-flavoured brassy stab with sharp attack.</summary>
    public static ISynthPatch CreateFMBrassLead()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithAggroDistortion(drive: 5f, stageRatio: .55f, bias: .1f)
            .WithHighPass(300f)
            .WithLowPass(92f)
            .WithPresenceShelf(+3f)
            .WithVolume(.78f)
            .WithEnvelope(0.0015, 0.07, 0.75, 0.22);

    /// <summary>Wide, detuned stack—good for anthem hooks.</summary>
    public static ISynthPatch CreateSuperSawStack()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithPitchDrift(.35f, 14f)
            .WithChorus(delayMs: 13, depthMs: 8, rateHz: .25f, mix: .36f)
            .WithHighPass(280f)
            .WithLowPass(100f)
            .WithPresenceShelf(+3f)
            .WithVolume(.8f)
            .WithEnvelope(0.003, 0.09, 0.85, 0.25);

    /// <summary>Shimmering keys with long decay and stereo tremolo.</summary>
    public static ISynthPatch CreateShimmerKeys()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithTremolo(depth: .45f, rateHz: 4f)
            .WithReverb(feedback: .78f, diffusion: .58f, wet: .35f, dry: .7f)
            .WithHighPass(320f)
            .WithVolume(.7f)
            .WithEnvelope(0.004, 0.18, 0.8, 1.3);

    /// <summary>Pulsing, gated arpeggio synth—sync tremolo to tempo.</summary>
    public static ISynthPatch CreateGatedArpSynth()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Square)
            .WithTremolo(depth: .65f, rateHz: 8f)           // 1/16-note gate @120 BPM
            .WithHighPass(360f)
            .WithLowPass(114f)
            .WithVolume(.8f)
            .WithEnvelope(0.002, 0.05, 0.8, 0.1);

    /// <summary>High-energy noise-layered lead for risers & screams.</summary>
    public static ISynthPatch CreateNoiseLead()
        => SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithAggroDistortion(drive: 7f)
            .WithHighPass(500f)
            .WithLowPass(143f)
            .WithTremolo(depth: .3f, rateHz: 1.8f)
            .WithVolume(.78f)
            .WithEnvelope(0.003, 0.06, 0.6, 0.4);
}