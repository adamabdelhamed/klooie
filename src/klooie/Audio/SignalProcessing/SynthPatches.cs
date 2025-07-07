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
        ret.EnableDistortion = false;              // Turn off harshness
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
        ret.EnableDistortion = true; // Add some growl
        ret.DistortionAmount = 0.2f; // Just enough for growl
        ret.EnablePitchDrift = false; // Stable bass
        ret.Envelope.Attack = 0.005;
        ret.Envelope.Decay = 0.12;
        ret.Envelope.Sustain = 0.5; // Increase sustain to avoid piano dropoff
        ret.Envelope.Release = 0.4;
        return ret;
    }
}