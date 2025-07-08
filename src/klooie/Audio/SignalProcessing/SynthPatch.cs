using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public interface ISynthPatch
{
    WaveformType Waveform { get; }
    float DriftFrequencyHz { get; }
    float DriftAmountCents { get; }
    bool EnablePitchDrift { get; }
    bool EnableSubOsc { get; }
    int SubOscOctaveOffset { get; }
    float SubOscLevel { get; }
    bool EnableTransient { get; }
    float TransientDurationSeconds { get; }
    float Velocity { get; }
    RecyclableList<IEffect> Effects { get; }

    void SpawnVoices(
        float frequencyHz,
        VolumeKnob master,
        VolumeKnob? sampleKnob,
        List<SynthSignalSource> outVoices);
}

public class SynthPatch : Recyclable, ISynthPatch
{
    private SynthPatch() { }
    private static LazyPool<SynthPatch> _pool = new(() => new SynthPatch());
    public static SynthPatch Create()
    {
        var patch = _pool.Value.Rent();
        patch.Waveform = WaveformType.Sine; // default waveform
        patch.EnableTransient = false;
        patch.TransientDurationSeconds = 0.01f; // default transient duration
        patch.EnableSubOsc = false; // default sub-oscillator disabled
        patch.SubOscLevel = 0.5f; // default sub-oscillator level
        patch.SubOscOctaveOffset = -1; // default sub-oscillator one octave below
        patch.EnablePitchDrift = false; // default pitch drift disabled
        patch.DriftFrequencyHz = 0.5f; // default drift frequency
        patch.DriftAmountCents = 5f; // default drift amount in cents
        patch.Velocity = 1f; // default velocity
        return patch;
    }

    public WaveformType Waveform { get; set; }
    public bool EnableTransient { get; set; }
    public float TransientDurationSeconds { get; set; }

    public bool EnableSubOsc { get; set; }
    public float SubOscLevel { get; set; } // 0 = silent, 1 = same as main
    public int SubOscOctaveOffset { get; set; } // usually -1 for one octave below


    public bool EnablePitchDrift { get; set; }
    public float DriftFrequencyHz { get; set; } // how fast the pitch wobbles
    public float DriftAmountCents { get; set; }   // how wide it wobbles (cents = 1/100 semitone)
    public float Velocity { get; set; } // default full velocity

    public RecyclableList<IEffect> Effects { get; set; } = RecyclableListPool<IEffect>.Instance.Rent(20);

    protected override void OnReturn()
    {
        base.OnReturn();
        for(var i = 0; i < Effects?.Count; i++)
        {
            if (Effects[i] is Recyclable r)
            {
                r.TryDispose();
            }
        }
        Effects.Dispose();
    }

    public virtual void SpawnVoices(
         float frequencyHz,
         VolumeKnob master,
         VolumeKnob? sampleKnob,
         List<SynthSignalSource> outVoices)
    {
        var innerVoice = SynthSignalSource.Create(frequencyHz, this, master, sampleKnob);
        this.OnDisposed(innerVoice, Recyclable.TryDisposeMe);
        outVoices.Add(innerVoice);
    }
}

public static class SynthPatchExtensions
{
    public static SynthPatch WithEffect(this SynthPatch patch, IEffect effect)
    {
        patch.Effects.Items.Add(effect);
        return patch;
    }

    public static SynthPatch WithReverb(this SynthPatch patch, float feedback = 0.78f, float diffusion = 0.5f, float wet = 0.3f, float dry = 0.7f)
        => patch.WithEffect(ReverbEffect.Create(feedback, diffusion, wet, dry));

    public static SynthPatch WithDelay(this SynthPatch patch, int delaySamples, float feedback = .3f, float mix = .4f)
        => patch.WithEffect(DelayEffect.Create(delaySamples, feedback, mix));

    public static SynthPatch WithChorus(this SynthPatch patch, int delayMs = 22, int depthMs = 7, float rateHz = 0.22f, float mix = 0.19f)
        => patch.WithEffect(StereoChorusEffect.Create(delayMs, depthMs, rateHz, mix));

    public static SynthPatch WithTremolo(this SynthPatch patch, float depth = 0.5f, float rateHz = 5f)
        => patch.WithEffect(TremoloEffect.Create(depth, rateHz));

    public static SynthPatch WithHighPass(this SynthPatch patch, float cutoffHz = 200f)
        => patch.WithEffect(HighPassFilterEffect.Create(cutoffHz));

    public static SynthPatch WithDistortion(this SynthPatch patch, float drive = 6f, float stageRatio = 0.6f, float bias = 0.15f)
        => patch.WithEffect(DistortionEffect.Create(drive, stageRatio, bias));
}

public interface IEffect
{
    // Process a mono sample (or stereo, if you want!)
    float Process(float input, int frameIndex, float time);
    IEffect Clone();
}

