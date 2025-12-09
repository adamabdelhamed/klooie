using System;
using PowerArgs;

namespace klooie;

[SynthCategory("Lead")]
[SynthDocumentation("""
Short, plinky toy-box / music-box style lead for nursery melodies.
Bright chime, fast decay, and slight detune for an eerie, wind-up
jack-in-the-box feel suitable for Krampus themes.
""")]
public static class ToyBox
{
    public static ISynthPatch Create(NoteExpression note)
    {
        // ---- Derived note context ----
        var bpm = note.BeatsPerMinute > 0 ? note.BeatsPerMinute : 120.0;
        var spb = 60.0 / bpm;                         // seconds per beat
        var durBeats = Math.Max(0.0, note.DurationBeats);
        var durSec = Math.Max(0.0, durBeats * spb);
        var vNorm = Math.Clamp(note.Velocity / 127f, 0f, 1f);
        var midi = note.MidiNote;

        // Helpers
        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
        static double LerpD(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);

        // Normalize note length for shaping envelopes
        var dur01 = (float)Math.Clamp(durBeats, 0.0, 1.0);

        // ---- Envelope (toy-box pluck) ----
        // Fast attack, short decay, almost no sustain, modest release.
        var attack = Lerp(0.001f, 0.003f, 1f - vNorm);                          // brighter at higher vel
        var decay = (float)Math.Clamp(LerpD(0.10, 0.25, dur01), 0.06, 0.28);    // short but can breathe a bit
        var sustain = Lerp(0.03f, 0.10f, dur01);                                // just enough tail to ring
        var release = (float)Math.Clamp(LerpD(0.06, 0.16, dur01), 0.05, 0.18);  // quick release to keep it tight

        // ---- Loudness ----
        // Keep this relatively modest; toy boxes are not huge.
        var patchVolMain = Lerp(0.020f, 0.040f, vNorm);
        var patchVolHigh = patchVolMain * 0.65f;

        // ---- Slight detune / width for "off" creepy vibe ----
        // Very small values so it still sounds like a toy box, not a pad.
        var detuneCentsMain = Lerp(2f, 6f, vNorm);          // tiny wobble
        var detuneCentsHigh = Lerp(4f, 9f, vNorm);
        var panSpreadMain = 0.25f;
        var panSpreadHigh = 0.35f;

        // ---- Pitch drift (subtle, evil doll energy) ----
        var driftDepth = Lerp(0.05f, 0.18f, dur01);         // very small, mostly for sustain notes
        var driftRate = Lerp(6.0f, 3.0f, dur01);            // slower drift on longer notes

        // ---- Reverb ----
        // Short, metallic-ish tail so it feels like a small mechanical box.
        var revDur = Lerp(0.14f, 0.22f, dur01);
        var revWet = Lerp(0.14f, 0.22f, dur01) * (0.80f + 0.20f * vNorm);
        var revDry = 0.86f;
        var revDamp = 0.12f;

        // Optional bright presence tilt for a chime-like top end.
        var presence = new PresenceSettings
        {
            GainDb = 3.0f.Param().WithClamp(-24f, +24f).WithSmoothing(0.40f)
                .Route(Env.Amp(), depth: +10.0f)
        };

        return LayeredPatch.CreateBuilder()
            // Base chime layer – sine / triangle-ish body
            .AddLayer
            (
                volume: 0.9f,
                pan: -0.08f,
                transpose: 0,
                patch: SynthPatch.Create(note)
                    .WithWaveForm(WaveformType.Triangle)                // slightly richer than sine
                    .WithVolume(patchVolMain)
                    .WithPresenceShelf(presence)
                    .WithEnvelope(delay: 0, attack: attack, decay: decay, sustainLevel: sustain, release: release)
                    .WithPitchDrift(driftDepth, driftRate)
                    .WrapWithUnison(numVoices: 2, detuneCents: detuneCentsMain, panSpread: panSpreadMain)
                    .WithReverb(duration: revDur, wet: revWet, dry: revDry, damping: revDamp)
            )
            // High octave sparkle – gives the classic music-box top
            .AddLayer
            (
                volume: 0.8f,
                pan: +0.10f,
                transpose: +12,
                patch: SynthPatch.Create(note)
                    .WithWaveForm(WaveformType.Sine)
                    .WithVolume(patchVolHigh)
                    .WithEnvelope(delay: 0, attack: attack, decay: decay * 0.8f, sustainLevel: sustain * 0.8f, release: release * 0.8f)
                    .WithPitchDrift(driftDepth * 0.7f, driftRate * 1.2f)
                    .WrapWithUnison(numVoices: 2, detuneCents: detuneCentsHigh, panSpread: panSpreadHigh)
                    .WithReverb(duration: revDur * 0.9f, wet: revWet * 0.9f, dry: revDry, damping: revDamp)
            )
            .Build();
    }
}
