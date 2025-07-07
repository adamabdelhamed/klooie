using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class SynthPatch : Recyclable
{
    private SynthPatch() { }
    private static LazyPool<SynthPatch> _pool = new(() => new SynthPatch());
    public static SynthPatch Create()
    {
        var patch = _pool.Value.Rent();
        patch.Waveform = WaveformType.Sine; // default waveform
        patch.Envelope = ADSREnvelope.Create();
        patch.EnableTransient = false;
        patch.TransientDurationSeconds = 0.01f; // default transient duration
        patch.EnableLowPassFilter = false;
        patch.FilterAlpha = 0.05f; // default filter alpha
        patch.EnableDynamicFilter = false;
        patch.FilterBaseAlpha = 0.01f; // default base alpha
        patch.FilterMaxAlpha = 0.2f; // default max alpha
        patch.EnableSubOsc = false; // default sub-oscillator disabled
        patch.SubOscLevel = 0.5f; // default sub-oscillator level
        patch.SubOscOctaveOffset = -1; // default sub-oscillator one octave below
        patch.EnableDistortion = false; // default distortion disabled
        patch.DistortionAmount = 0.8f; // default distortion amount
        patch.EnablePitchDrift = false; // default pitch drift disabled
        patch.DriftFrequencyHz = 0.5f; // default drift frequency
        patch.DriftAmountCents = 5f; // default drift amount in cents
        patch.Velocity = 1f; // default velocity
        return patch;
    }

    public WaveformType Waveform;
    public ADSREnvelope Envelope;
    public bool EnableTransient;
    public float TransientDurationSeconds;
    public bool EnableLowPassFilter;
    public float FilterAlpha;

    public bool EnableDynamicFilter;
    public float FilterBaseAlpha;
    public float FilterMaxAlpha;

    public bool EnableSubOsc;
    public float SubOscLevel; // 0 = silent, 1 = same as main
    public int SubOscOctaveOffset; // usually -1 for one octave below

    public bool EnableDistortion;
    public float DistortionAmount; // 0 = clean, 1 = hard clip

    public bool EnablePitchDrift;
    public float DriftFrequencyHz; // how fast the pitch wobbles
    public float DriftAmountCents;   // how wide it wobbles (cents = 1/100 semitone)
    public float Velocity; // default full velocity

    protected override void OnReturn()
    {
        base.OnReturn();
        Envelope.Dispose();
        Envelope = null!;
    }
}
