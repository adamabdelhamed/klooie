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
    int Velocity { get; }
    RecyclableList<IEffect> Effects { get; }
    ISynthPatch InnerPatch { get; }
    void SpawnVoices(float frequencyHz, VolumeKnob master, VolumeKnob? sampleKnob, List<SynthSignalSource> outVoices);
}

public class SynthPatch : Recyclable, ISynthPatch
{
    public ISynthPatch InnerPatch => this;
    private SynthPatch() { }
    private static LazyPool<SynthPatch> _pool = new(() => new SynthPatch());
    public static SynthPatch Create()
    {
        var patch = _pool.Value.Rent();
        patch.Waveform = WaveformType.Sine; 
        patch.EnableTransient = false;
        patch.EnableSubOsc = false;
        patch.EnablePitchDrift = false; 
        patch.Velocity = 127;
        return patch;
    }

    public WaveformType Waveform { get; set; }
    public bool EnableTransient { get; set; }
    public float TransientDurationSeconds { get; set; }

    public bool EnableSubOsc { get; set; }
    public float SubOscLevel { get; set; }  
    public int SubOscOctaveOffset { get; set; }  


    public bool EnablePitchDrift { get; set; }
    public float DriftFrequencyHz { get; set; }  
    public float DriftAmountCents { get; set; }    
    public int Velocity { get; set; }  

    public RecyclableList<IEffect> Effects { get; set; } = RecyclableListPool<IEffect>.Instance.Rent(20);

    protected override void OnReturn()
    {
        base.OnReturn();
        for(var i = 0; i < Effects?.Count; i++)
        {
            if (Effects[i] is Recyclable r) r.TryDispose();
        }
        Effects.Dispose();
    }

    public virtual void SpawnVoices(float frequencyHz, VolumeKnob master, VolumeKnob? sampleKnob, List<SynthSignalSource> outVoices)
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

    public static SynthPatch WithCabinet(this SynthPatch patch)
        => patch.WithEffect(CabinetEffect.Create());

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

    public static SynthPatch WithLowPass(this SynthPatch patch, float alpha)
        => patch.WithEffect(LowPassFilterEffect.Create(alpha));

    public static SynthPatch WithDistortion(this SynthPatch patch, float drive = 6f, float stageRatio = 0.6f, float bias = 0.15f)
        => patch.WithEffect(DistortionEffect.Create(drive, stageRatio, bias));
    public static SynthPatch WithAggroDistortion(this SynthPatch patch, float drive = 12f, float stageRatio = 0.8f, float bias = 0.12f)
    => patch.WithEffect(AggroDistortionEffect.Create(drive, stageRatio, bias));
    public static SynthPatch WithVolume(this SynthPatch patch, float volume = 1.0f)
     => patch.WithEffect(VolumeEffect.Create(volume));

    public static SynthPatch WithNoiseGate(this SynthPatch patch, float openThresh = 0.05f, float closeThresh = 0.04f, float attackMs = 2f, float releaseMs = 60f) 
        => patch.WithEffect(NoiseGateEffect.Create(openThresh, closeThresh, attackMs, releaseMs));

    public static SynthPatch WithTransient(this SynthPatch patch, float transientDurationSeconds = .01f)
    {
        patch.EnableTransient = true;
        patch.TransientDurationSeconds = transientDurationSeconds;
        return patch;
    }

    public static SynthPatch WithPitchDrift(this SynthPatch patch, float driftFrequencyHz = 0.5f, float driftAmountCents = 5f)
    {
        patch.EnablePitchDrift = true;
        patch.DriftFrequencyHz = driftFrequencyHz;
        patch.DriftAmountCents = driftAmountCents;
        return patch;
    }

    public static SynthPatch WithSubOscillator(this SynthPatch patch, float subOscLevel = .5f, int subOscOctaveOffset = -1)
    {
        patch.SubOscLevel = subOscLevel;
        patch.SubOscOctaveOffset = subOscOctaveOffset; 
        return patch;
    }

    public static SynthPatch WithWaveForm(this SynthPatch patch, WaveformType waveform)
    {
        patch.Waveform = waveform;
        return patch;
    }

    public static SynthPatch WithFadeIn(this SynthPatch patch, float durationSeconds = 1.5f)
    {
        patch.WithEffect(FadeInEffect.Create(durationSeconds));
        return patch;
    }

    public static SynthPatch WithFadeOut(this SynthPatch patch, float durationSeconds = 1.5f)
    {
        patch.WithEffect(FadeOutEffect.Create(durationSeconds));
        return patch;
    }

    public static SynthPatch WithEnvelope(this SynthPatch patch, double attackMs, double decayMs, double sustainLevel, double releaseMs)
    {
        patch.WithEffect(EnvelopeEffect.Create(attackMs, decayMs, sustainLevel, releaseMs));
        return patch;
    }

    public static SynthPatch WithToneStack(this SynthPatch patch,
                                       float bass = 1f,
                                       float mid = 1f,
                                       float treble = 1f)
    => patch.WithEffect(ToneStackEffect.Create(bass, mid, treble));

    public static SynthPatch WithPresenceShelf(this SynthPatch p, float presenceDb = +3f)
    => p.WithEffect(PresenceShelfEffect.Create(presenceDb));

    public static SynthPatch WithPickTransient(this SynthPatch p,
                                           float dur = .005f, float gain = .6f)
    => p.WithEffect(PickTransientEffect.Create(dur, gain));

    public static SynthPatch WithDCBlocker(this SynthPatch p)
    => p.WithEffect(DCBlockerEffect.Create());

    public static SynthPatch WithPeakEQ(this SynthPatch patch, float freq, float gainDb, float q = 1.0f)
    => patch.WithEffect(ParametricEQEffect.Create(BiquadType.Peak, freq, gainDb, q));

    public static SynthPatch WithLowShelf(this SynthPatch patch, float freq, float gainDb)
        => patch.WithEffect(ParametricEQEffect.Create(BiquadType.LowShelf, freq, gainDb));

    public static SynthPatch WithHighShelf(this SynthPatch patch, float freq, float gainDb)
        => patch.WithEffect(ParametricEQEffect.Create(BiquadType.HighShelf, freq, gainDb));

    public static EnvelopeEffect? FindEnvelopeEffect(this ISynthPatch patch)
    {
        // Unwrap until we get a patch whose InnerPatch == itself
        ISynthPatch cur = patch;
        while (true)
        {
            if (cur is SynthPatch s && s.Effects.Items != null)
            {
                for(var i = 0; i < s.Effects.Items.Count; i++)
                {
                    if (s.Effects.Items[i] is EnvelopeEffect env) return env;
                }

            }

            var next = cur.InnerPatch;
            if (next == null || next == cur) break;
            cur = next;
        }
        return null;
    }

    public static SynthPatch? GetLeafSynthPatch(this ISynthPatch patch)
    {
        while (patch is not SynthPatch)
        {
            patch = patch.InnerPatch; // You have this property!
            if (patch == null) return null;
        }
        return patch as SynthPatch;
    }
}

public interface IEffect
{
    // Process a mono sample (or stereo, if you want!)
    float Process(float input, int frameIndex, float time);
    IEffect Clone();
}

