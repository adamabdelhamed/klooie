using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DAWApp : ConsoleApp
{
    protected override async Task Startup()
    {
        await base.Startup();
        var pianoWithTimeline = LayoutRoot.Add(new PianoWithTimeline(new DemoSong(120))).Fill();
        pianoWithTimeline.Timeline.RefreshVisibleSet();
        pianoWithTimeline.Timeline.Focus();
    }
}


public class DemoSong : Song
{

    private ISynthPatch AddCustomPitchBend(ISynthPatch patch, int noteBeats, bool up)
    {
        float noteSeconds = noteBeats * (60f / (float)this.BeatsMerMinute);
        Func<float, float> bendFunc = t =>
        {
            float frac = t / noteSeconds; // normalize time [0..1]
            if (frac < 0.7f) return 0f;
            float bendFrac = Math.Clamp((frac - 0.7f) / 0.3f, 0f, 1f);
            float shaped = bendFrac * bendFrac * bendFrac; // cubic ease-in
            return shaped * (up ? 20 : -70);
        };
        patch.WithPitchBend(bendFunc, noteSeconds);
        return patch;
    }

    public DemoSong(int bpm) : base(bpm)
    {
        var PadNotes = new NoteCollection([
    NoteExpression.Create(43, 16, 70, CreateSoftLayeredPad),   // G2, long
    NoteExpression.Create(39, 16, 55, CreateSoftLayeredPad),   // D3, long
    // You can layer more for complex harmony, or duplicate at higher octaves
]).WithInstrument(CreateSoftLayeredPad);

        var BassPattern = new NoteCollection(
        [
            NoteExpression.Create(39, .5, 90),
            NoteExpression.Create(37, 1,  100),
            NoteExpression.Create(39, .5, 110), NoteExpression.Rest(1.5),
            NoteExpression.Create(39, .5, 100), NoteExpression.Rest(1.5),
            NoteExpression.Create(39, .5, 100), NoteExpression.Rest(2),
        ]).WithInstrument(CreateFatLayeredBass).WithOctave(0);

        var MelodyRoot = new NoteCollection(
        [
            NoteExpression.Create(61, .75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(63, .75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(61, .75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(63, 1, 100), NoteExpression.Rest(1),
            NoteExpression.Create(63, 1, 100), NoteExpression.Rest(1),
            NoteExpression.Create(66, .75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(68, 1.5, 100), NoteExpression.Rest(.5),
        ]).WithInstrumentIfNull(MelodyInstrument);

        var MelodyUp = MelodyRoot.AddSequential(new NoteCollection(
        [
            NoteExpression.Create(73, 1.75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(70, 2, 120), NoteExpression.Rest(2),
        ]).WithInstrumentIfNull(MelodyInstrument));

        var MelodyDown = MelodyRoot.AddSequential(new NoteCollection(
        [
            NoteExpression.Create(65, 1.75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(63, 2, 120), NoteExpression.Rest(2),
        ]).WithInstrumentIfNull(MelodyInstrument));



        var MelodyBridge = new NoteCollection(
        [
            NoteExpression.Create(63, 1, 120), NoteExpression.Rest(1),

            NoteExpression.Create(61, .75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(63, 1.25, 100), NoteExpression.Rest(.75),
            NoteExpression.Create(65, 1.25, 100), NoteExpression.Rest(.75),
            NoteExpression.Create(66, 2, 100), NoteExpression.Rest(3),

            NoteExpression.Create(65, 1.5, 100), NoteExpression.Rest(.5),
            NoteExpression.Create(68, 2, 100), NoteExpression.Rest(4),

            NoteExpression.Create(61, 2.5, 100), NoteExpression.Rest(.5),
            NoteExpression.Create(63, 1.5, 100), NoteExpression.Rest(3.5),

            NoteExpression.Create(61, 1.5, 100), NoteExpression.Rest(.5),
            NoteExpression.Create(63, 1.5, 100), NoteExpression.Rest(4.5),

        ]).WithInstrumentIfNull(MelodyInstrument);





        var MelodyBuild = MelodyRoot.AddSequential(new NoteCollection(
        [
            NoteExpression.Rest(1),
            NoteExpression.Create(73, .75, 100), NoteExpression.Rest(.25),
            NoteExpression.Create(75, 1.5, 100), NoteExpression.Rest(.5),
            NoteExpression.Create(78, 1.5, 100), NoteExpression.Rest(.5),
            NoteExpression.Create(75, 1.5, 100),

        ]).WithInstrumentIfNull(MelodyInstrument));

        var MelodyResolve = new NoteCollection(
        [
            NoteExpression.Rest(.5),
            NoteExpression.Create(70, 1.75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(73, .75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(75, .75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(73, 1.75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(70, 1, 120), NoteExpression.Rest(1),

            NoteExpression.Create(66, 1.75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(68, .75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(70, .75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(68, 1.75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(66, 1, 120), NoteExpression.Rest(1),

            NoteExpression.Create(61, .75, 120), NoteExpression.Rest(.25),
            NoteExpression.Create(63, 1, 120), NoteExpression.Rest(4),

        ]).WithInstrumentIfNull(MelodyInstrument);

        var DrumPattern = new NoteCollection(
        [
            // Bars 1-2: Build up with kick and hats
            NoteExpression.Create(36, 0.5, 127, DrumPatches.Kick),    // Kick
            NoteExpression.Rest(0.5),
            NoteExpression.Create(42, 0.5, 95, DrumPatches.ClosedHat), // Closed hat
            NoteExpression.Rest(0.5),
            NoteExpression.Create(36, 0.5, 127, DrumPatches.Kick),
            NoteExpression.Create(42, 0.5, 95, DrumPatches.ClosedHat),
            NoteExpression.Rest(0.5),
            NoteExpression.Create(38, 0.5, 105, DrumPatches.Snare),    // Snare
            NoteExpression.Rest(0.5),

            // Bar 3: Add open hats, clap, perc
            NoteExpression.Create(36, 0.5, 127, DrumPatches.Kick),
            NoteExpression.Create(46, 0.5, 90, DrumPatches.OpenHat),   // Open hat
            NoteExpression.Create(42, 0.25, 80, DrumPatches.ClosedHat),
            NoteExpression.Create(39, 0.25, 85, DrumPatches.Perc),     // Perc click
            NoteExpression.Create(38, 0.5, 110, DrumPatches.Snare),
            NoteExpression.Create(39, 0.25, 90, DrumPatches.Clap),     // Clap (layered)
            NoteExpression.Rest(0.25),
        ]);


        var StandardMelodyPhrase = MelodyUp.AddSequential(MelodyDown);
        var DelayedStandardMelodyPhrase = new NoteCollection([NoteExpression.Rest(30.5)]).AddSequential(StandardMelodyPhrase);

        var FirstVerse = BassPattern.Repeat(16)
                                        .AddParallel(DelayedStandardMelodyPhrase.AddSequential(StandardMelodyPhrase).AddSequential(MelodyBridge, 2).AddSequential(MelodyBuild).AddSequential(MelodyResolve).WithOctave(2))
                                        .AddParallel(DrumPattern.Repeat(18))
                                        .AddParallel(PadNotes.Repeat(5));

        Notes = FirstVerse;
    }

    private static Func<ISynthPatch> MelodyInstrument => () => SynthPatches.CreateGuitar().WithVolume(5);

    public static ISynthPatch CreateSoftLayeredPad()
    {
        var pad1 = CreateSoftAmbientPad();

        var shimmer = SynthPatch.Create()
            .WithWaveForm(WaveformType.Saw)
            .WithHighPass(1500f)
            .WithLowPass(0.12f)
            .WithChorus(12, 8, 0.21f, 0.18f)
            .WithEnvelope(0.21, 0.7, 0.27, 2.1)
            .WithVolume(0.4f);

        return LayeredPatch.Create(
            patches: [pad1, shimmer],
            volumes: [1.0f, 0.25f],
            pans: [0.0f, 0.0f]
        ).WithVolume(0.82f);
    }

    public static ISynthPatch CreateSoftAmbientPad()
    {
        return UnisonPatch.Create(
            numVoices: 4,
            detuneCents: 7.5f,
            panSpread: 0.29f,
            basePatch: SynthPatch.Create()
                .WithWaveForm(WaveformType.Triangle)
                .WithPitchDrift(0.11f, 4f)
                .WithHighPass(90f)
                .WithLowPass(0.022f)
                .WithChorus(16, 6, 0.18f, 0.14f)
                .WithReverb(0.52f, 0.28f, 0.11f, 0.91f)
                .WithEnvelope(0.22, 1.4, 0.6, 2.4)
                .WithVolume(0.58f)
        );
    }



    public static ISynthPatch CreateFatLayeredBass() => LayeredPatch.Create(
            patches:
            [
                UnisonPatch.Create(
                    numVoices: 2,
                    detuneCents: 3.5f,
                    panSpread: 0.18f,
                    basePatch: PowerChordPatch.Create(
                        basePatch: SynthPatch.Create()
                            .WithWaveForm(WaveformType.Square)
                            .WithPickTransient(.003f, .45f)
                            .WithLowPass(.014f)
                            .WithAggroDistortion(5f, 0.7f, 0.08f)
                            .WithNoiseGate(.02f, .018f)
                            .WithVolume(.70f)
                            .WithEnvelope(0.002, 0.045, 0.45, 0.13),
                        intervals: [0, -12],
                        detuneCents: 0f,
                        panSpread: 0f)),
                SynthPatch.Create()
                    .WithWaveForm(WaveformType.Noise)
                    .WithHighPass(900f)
                    .WithLowPass(.12f)
                    .WithPickTransient(.002f, .75f)
                    .WithVolume(.15f)
                    .WithEnvelope(0.0007, 0.009, 0.05, 0.02),
                SynthPatch.Create()
                    .WithWaveForm(WaveformType.Sine)
                    .WithLowPass(.008f)
                    .WithVolume(.38f)
                    .WithEnvelope(0.002, 0.05, 0.8, 0.16)
            ],
            volumes: [1.0f, 0.9f],
            pans: [0.0f, 0.0f]
        ).WithVolume(.3f);
}

public static class DrumPatches
{
    public static ISynthPatch Kick() =>
        SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithTransient(0.005f)
            .WithDistortion(2.5f, 0.4f, 0.1f)
            .WithHighPass(24f)
            .WithLowPass(0.10f)
            .WithEnvelope(0.002f, 0.09f, 0.0f, 0.07f)
            .WithVolume(2f);

    public static ISynthPatch Snare() =>
        SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithHighPass(900f)
            .WithLowPass(0.15f)
            .WithReverb(0.41f, 0.09f, 0.16f, 0.85f)
            .WithEnvelope(0.0012f, 0.041f, 0.0f, 0.09f)
            .WithVolume(0.8f);

    public static ISynthPatch ClosedHat() =>
        SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithHighPass(2400f)
            .WithLowPass(0.022f)
            .WithEnvelope(0.0009f, 0.018f, 0.0f, 0.022f)
            .WithVolume(0.6f);

    public static ISynthPatch OpenHat() =>
        SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithHighPass(2100f)
            .WithLowPass(0.11f)
            .WithEnvelope(0.0015f, 0.11f, 0.0f, 0.15f)
            .WithVolume(0.6f);

    public static ISynthPatch Clap() =>
        SynthPatch.Create()
            .WithWaveForm(WaveformType.Noise)
            .WithHighPass(1300f)
            .WithLowPass(0.17f)
            .WithReverb(0.24f, 0.12f, 0.13f, 0.73f)
            .WithEnvelope(0.0012f, 0.03f, 0.0f, 0.05f)
            .WithVolume(0.6f);

    public static ISynthPatch Perc() =>
        SynthPatch.Create()
            .WithWaveForm(WaveformType.Sine)
            .WithHighPass(500f)
            .WithEnvelope(0.0012f, 0.02f, 0.0f, 0.04f)
            .WithVolume(0.6f);
}
