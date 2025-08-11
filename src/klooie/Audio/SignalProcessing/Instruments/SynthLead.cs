using System.Collections.Generic;
using PowerArgs;
using System;

namespace klooie;

[SynthCategory("Lead")]
[SynthDocumentation("""
Wide, aggressive synth lead designed for heroic melodies. Combines detuned saw stacks,
harmonic square layers and octave-sub reinforcement with time-synced modulation,
chorus, delay and reverb for an epic, evolving tone.
""")]
public static class SynthLead
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

        // Low notes => reduce width/detune a bit to keep definition.
        // Map midi 36..60 => 0.6 .. 1.0
        float LowNoteFactor(int m)
        {
            var t = (m - 36f) / (60f - 36f);
            return Math.Clamp(t, 0f, 1f) * 0.4f + 0.6f;
        }
        var lowFac = LowNoteFactor(midi);

        // Helpers
        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
        static double LerpD(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);

        // Normalize "is this a short stab?" on a 1-beat scale
        var dur01 = (float)Math.Clamp(durBeats, 0.0, 1.0);

        // ---- Envelope (adaptive) ----
        // Faster attack for higher velocity; decay scales with note length; sustain stays high for a lead; release scales with length.
        var attack = Lerp(0.003f, 0.001f, vNorm);                                         // 3ms -> 1.5ms
        var decay = (float)Math.Clamp(LerpD(0.025, 0.12, dur01), 0.03, 0.18);             // ~short -> longer
        var sustain = Lerp(0.88f, 0.96f, vNorm);                                           // more push on high velocity
        var release = (float)Math.Clamp(LerpD(0.10, 0.30, dur01), 0.08, 0.45);             // stabs don’t smear; long notes breathe

        // ---- Loudness (adaptive) ----
        // Base layer gain stays ~constant; patch-level volume responds to velocity.
        var patchVol = Lerp(0.022f, 0.055f, vNorm);

        // ---- Unison/Width (adaptive) ----
        var detuneCents = Lerp(16f, 30f, vNorm) * lowFac;                                  // less detune on low notes
        var panSpread = Lerp(0.75f, 1.0f, vNorm * lowFac);

        // ---- Pitch drift (adaptive) ----
        // Longer notes drift a little deeper but slower.
        var driftDepth = Lerp(0.45f, 0.90f, dur01);
        var driftRate = Lerp(5.5f, 3.5f, dur01);

        // ---- Chorus (adaptive) ----
        var chorusDelayMs = Lerp(18f, 28f, vNorm);                                         // subtle widening at low vel, richer at high
        var chorusDepthMs = Lerp(4f, 7f, vNorm);
        var chorusRateHz = Lerp(0.22f, 0.32f, vNorm);
        var chorusMix = Lerp(0.18f, 0.28f, vNorm * lowFac);

        // ---- Tempo-synced delay (dotted eighth) ----
        var delayBeats = 0.75;                                                             // dotted-8th
        var delayMs = (float)(delayBeats * spb * 1000.0);
        var delayFb = Lerp(0.36f, 0.46f, dur01);
        var delayMix = Lerp(0.24f, 0.38f, MathF.Min(1f, vNorm * (float)Math.Sqrt(dur01 + 1e-6f)));

        // ---- Reverb (adaptive) ----
        // Slightly longer and wetter for sustained notes; keep dry high to stay forward in the mix.
        var revDur = Lerp(0.18f, 0.55f, dur01);
        var revWet = Lerp(0.08f, 0.18f, dur01) * (0.85f + 0.15f * vNorm);
        var revDry = 0.80f;
        var revDamp = 0.22f;

        return LayeredPatch.CreateBuilder()
            .AddLayer
            (
                volume: 0.9f, pan: -0.4f, transpose: 0,
                patch: SynthPatch.Create(note)
                    .WithWaveForm(WaveformType.Saw)
                    .WithVolume(patchVol)
                    .WithEnvelope(delay: 0, attack: attack, decay: decay, sustainLevel: sustain, release: release)
                    .WithDCBlocker()
                    .WithPitchDrift(driftDepth, driftRate)
                    .WrapWithUnison(numVoices: 3, detuneCents: detuneCents, panSpread: panSpread)
                    .WrapWithPowerChord([0, 7, 12])
                    .WithChorus(delayMs: (int)chorusDelayMs, depthMs: (int)chorusDepthMs, rateHz: chorusRateHz, mix: chorusMix)
                    .WithPingPongDelay(delayMs: delayMs, feedback: delayFb, mix: delayMix)
                    .WithReverb(duration: revDur, wet: revWet, dry: revDry, damping: revDamp)
            )
            .Build();
    }
}
