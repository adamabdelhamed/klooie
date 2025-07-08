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
        ret.EnableSubOsc = false;                  // Skip for realism
        ret.EnablePitchDrift = false;              // Plucked strings are stable
        ret.TransientDurationSeconds = 0.01f;      // Ignored here but safe to leave
        ret.WithEffect(LowPassFilterEffect.Create(alpha: 0.02f));
        ret.WithEffect(EnvelopeEffect.Create(.005f, 0.1f, 0.25f, 0.4f));
        return ret;
    }

    public static SynthPatch CreateBass()
    {
        var ret = SynthPatch.Create();
        ret.Waveform = WaveformType.Sine; // Smooth bass sound
        ret.EnableTransient = false; // No transient burst needed
        ret.EnableSubOsc = true; // Add sub-oscillator for depth
        ret.SubOscLevel = 0.6f; // Moderate sub-oscillator level
        ret.SubOscOctaveOffset = -1; // One octave below
        ret.WithEffect(LowPassFilterEffect.Create(.01f));
        ret.WithDistortion();
        ret.EnablePitchDrift = false; // Stable bass
        ret.WithEffect(EnvelopeEffect.Create(0.005f, 0.12f, 0.5f, 0.4f));
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
 
        // Add chorus and reverb effects
        patch.WithEffect(StereoChorusEffect.Create(delayMs: 22, depthMs: 7, rateHz: 0.22f, mix: 0.19f));
        patch.WithReverb(feedback: 0.73f, diffusion: 0.52f, wet: 0.26f, dry: 0.74f);
        patch.WithEffect(EnvelopeEffect.Create(0.23f, 1.3f, 0.85f, 1.6f));
        return patch;
    }

    public static SynthPatch CreateLead()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Saw;
        patch.EnablePitchDrift = true;
        patch.DriftFrequencyHz = 0.2f;
        patch.DriftAmountCents = 3f;
        patch.WithDistortion();
        patch.WithEffect(LowPassFilterEffect.Create(alpha: 0.03f)); 
        patch.WithChorus();
        patch.WithReverb();
        patch.WithEffect(EnvelopeEffect.Create(0.01f, 0.15f, 0.6f, 0.25f));
        return patch;
    }

    public static SynthPatch CreateKick()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Sine;
        patch.EnableTransient = true;
        patch.TransientDurationSeconds = 0.005f;
        patch.WithDistortion();
        patch.WithHighPass(20f);
        patch.WithEffect(EnvelopeEffect.Create(0, 0.1f, 0.0f, 0.1f));
        return patch;
    }

    public static SynthPatch CreateSnare()
    {
        var patch = SynthPatch.Create();
        patch.Waveform = WaveformType.Noise;
        patch.EnableTransient = true;
        patch.TransientDurationSeconds = 0.005f;
        patch.WithDistortion();
        patch.WithEffect(LowPassFilterEffect.Create(alpha: 0.2f));
        patch.WithHighPass(1000f);
        patch.WithReverb();
        patch.WithEffect(EnvelopeEffect.Create(0, .1, 0, 0.2f));
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
        patch.WithTremolo(depth: 0.5f, rateHz: 5f);
        patch.WithEffect(LowPassFilterEffect.Create(alpha: 0.015f));
        patch.WithReverb();
        patch.WithEffect(EnvelopeEffect.Create(0.5f, 1.0f, 0.75f, 1.5f));
        return patch;
    }


    public static ISynthPatch CreateRockGuitar()
    {
        var basePatch = SynthPatch.Create();
        // ... set up waveform, envelope, effects as before ...

        basePatch.Waveform = WaveformType.Saw;
        basePatch.EnablePitchDrift = true;
        basePatch.DriftFrequencyHz = 0.5f;
        basePatch.DriftAmountCents = 4f;
        basePatch.WithEffect(LowPassFilterEffect.Create(alpha: 0.02f));
        basePatch.WithEffect(NoiseGateEffect.Create(releaseMs: 140f));
        basePatch.WithDistortion(10);
        basePatch.WithEffect(CabinetEffect.Create());
        basePatch.WithChorus(delayMs: 22, depthMs: 10, rateHz: 0.23f, mix: 0.28f);
        basePatch.WithReverb(feedback: 0.70f, diffusion: 0.52f, wet: 0.28f, dry: 0.68f);
        basePatch.WithEffect(EnvelopeEffect.Create(0.012f, 0.18f, 0.75f, 0.9f));
        // Wrap in unison patch for instant width and analog vibe:
        return UnisonPatch.Create(
            basePatch,
            numVoices: 3,     // or 2 for less width
            detuneCents: 7f,  // 7 cents = classic
            panSpread: 0.8f   // adjust for stereo field width
        );
    }
}