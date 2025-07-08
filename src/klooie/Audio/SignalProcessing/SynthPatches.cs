using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class SynthPatches
{
    public static SynthPatch CreateGuitar()
    {
        var ret = SynthPatch.Create();
        ret.Waveform = WaveformType.PluckedString; // New waveform
        ret.EnableTransient = false;               // Let the pluck algorithm handle the burst
        ret.EnableLowPassFilter = true;            // Smooth the highs
        ret.EnableDynamicFilter = false;           // Keep static filtering for simplicity
        ret.FilterAlpha = 0.02f;
        ret.EnableSubOsc = false;                  // Skip for realism
        ret.EnablePitchDrift = false;              // Plucked strings are stable
        ret.TransientDurationSeconds = 0.01f;      // Ignored here but safe to leave
        ret.Envelope.Attack = 0.005;
        ret.Envelope.Decay = 0.1;
        ret.Envelope.Sustain = 0.25;
        ret.Envelope.Release = 0.4;
        return ret;
    }

    public static SynthPatch CreateBass()
    {
        var ret = SynthPatch.Create();
        ret.Waveform = WaveformType.Sine; // Smooth bass sound
        ret.EnableTransient = false; // No transient burst needed
        ret.EnableLowPassFilter = true; // Smooth out highs
        ret.EnableDynamicFilter = false; // Keep static filtering
        ret.FilterAlpha = 0.01f; // Let more mids through post-distortion
        ret.EnableSubOsc = true; // Add sub-oscillator for depth
        ret.SubOscLevel = 0.6f; // Moderate sub-oscillator level
        ret.SubOscOctaveOffset = -1; // One octave below
        ret.WithDistortion();
        ret.EnablePitchDrift = false; // Stable bass
        ret.Envelope.Attack = 0.005;
        ret.Envelope.Decay = 0.12;
        ret.Envelope.Sustain = 0.5; // Increase sustain to avoid piano dropoff
        ret.Envelope.Release = 0.4;
        return ret;
    }

    public static SynthPatch CreateAnalogPad()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Saw;
        patch.EnablePitchDrift = true;
        patch.DriftFrequencyHz = 0.3f;   // Slow drift
        patch.DriftAmountCents = 7f;     // Light chorus
        patch.SubOscLevel = 0.5f;
        patch.SubOscOctaveOffset = -1; // Lower octave
        patch.EnableLowPassFilter = true;
        patch.FilterBaseAlpha = 0.012f;   // Smooth highs
        patch.FilterMaxAlpha = 0.05f;

        patch.Envelope.Attack = 0.23;
        patch.Envelope.Decay = 1.3;
        patch.Envelope.Sustain = 0.85;
        patch.Envelope.Release = 1.6;

        // Add chorus and reverb effects
        patch.WithEffect(StereoChorusEffect.Create(delayMs: 22, depthMs: 7, rateHz: 0.22f, mix: 0.19f));
        patch.WithReverb(feedback: 0.73f, diffusion: 0.52f, wet: 0.26f, dry: 0.74f);

        return patch;
    }

    public static SynthPatch CreateLead()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Saw;
        patch.EnablePitchDrift = true;
        patch.DriftFrequencyHz = 0.2f;
        patch.DriftAmountCents = 3f;
        patch.EnableLowPassFilter = true;
        patch.FilterAlpha = 0.03f;
        patch.WithDistortion();
        patch.Envelope.Attack = 0.01;
        patch.Envelope.Decay = 0.15;
        patch.Envelope.Sustain = 0.6;
        patch.Envelope.Release = 0.25;
        patch.WithChorus();
        patch.WithReverb();
        return patch;
    }

    public static SynthPatch CreateKick()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Sine;
        patch.EnableTransient = true;
        patch.TransientDurationSeconds = 0.005f;
        patch.WithDistortion();
        patch.Envelope.Attack = 0.0;
        patch.Envelope.Decay = 0.1;
        patch.Envelope.Sustain = 0.0;
        patch.Envelope.Release = 0.1;
        patch.WithHighPass(20f);
        return patch;
    }

    public static SynthPatch CreateSnare()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Noise;
        patch.EnableTransient = true;
        patch.TransientDurationSeconds = 0.005f;
        patch.EnableLowPassFilter = true;
        patch.FilterAlpha = 0.2f;
        patch.WithDistortion();
        patch.Envelope.Attack = 0.0;
        patch.Envelope.Decay = 0.1;
        patch.Envelope.Sustain = 0.0;
        patch.Envelope.Release = 0.2;
        patch.WithHighPass(1000f);
        patch.WithReverb();
        return patch;
    }

    public static SynthPatch CreateRhythmicPad()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Saw;
        patch.EnablePitchDrift = true;
        patch.DriftFrequencyHz = 0.15f;
        patch.DriftAmountCents = 5f;
        patch.EnableSubOsc = true;
        patch.SubOscLevel = 0.5f;
        patch.SubOscOctaveOffset = -1;
        patch.EnableLowPassFilter = true;
        patch.FilterBaseAlpha = 0.015f;
        patch.FilterMaxAlpha = 0.05f;
        patch.EnableDynamicFilter = true;
        patch.Envelope.Attack = 0.5;
        patch.Envelope.Decay = 1.0;
        patch.Envelope.Sustain = 0.75;
        patch.Envelope.Release = 1.5;
        patch.WithTremolo(depth: 0.5f, rateHz: 5f);
        patch.WithReverb();
        return patch;
    }


    public static ISynthPatch CreateRockGuitar()
    {
        var basePatch = SynthPatch.Create();
        // ... set up waveform, envelope, effects as before ...

        basePatch.Waveform = WaveformType.Saw;
        basePatch.EnableLowPassFilter = true;
        basePatch.FilterAlpha = 0.02f;
        basePatch.EnablePitchDrift = true;
        basePatch.DriftFrequencyHz = 0.5f;
        basePatch.DriftAmountCents = 4f;
        basePatch.Envelope.Attack = 0.012;
        basePatch.Envelope.Decay = 0.18;
        basePatch.Envelope.Sustain = 0.75;
        basePatch.Envelope.Release = 0.9;
        basePatch.WithEffect(NoiseGateEffect.Create(releaseMs: 140f));
        basePatch.WithDistortion(10);
        basePatch.WithEffect(CabinetEffect.Create());
        basePatch.WithChorus(delayMs: 22, depthMs: 10, rateHz: 0.23f, mix: 0.28f);
        basePatch.WithReverb(feedback: 0.70f, diffusion: 0.52f, wet: 0.28f, dry: 0.68f);

        // Wrap in unison patch for instant width and analog vibe:
        return UnisonPatch.Create(
            basePatch,
            numVoices: 3,     // or 2 for less width
            detuneCents: 7f,  // 7 cents = classic
            panSpread: 0.8f   // adjust for stereo field width
        );
    }
}