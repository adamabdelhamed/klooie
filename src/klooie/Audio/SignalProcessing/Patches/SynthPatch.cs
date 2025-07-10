using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public interface ICompositePatch : ISynthPatch { IEnumerable<ISynthPatch> Patches { get; } }
public interface ISynthPatch
{
    bool IsNotePlayable(int midiNote) => true;  // default: always playable
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

    public bool EnableVibrato { get;  }
    public float VibratoRateHz { get;  }
    public float VibratoDepthCents { get;  }
    public float VibratoPhaseOffset { get;  }

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

    public bool EnableVibrato { get; set; }
    public float VibratoRateHz { get; set; }
    public float VibratoDepthCents { get; set; }
    public float VibratoPhaseOffset { get; set; }

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
    // ----- Effect: applies to all leaves -----

    public static ISynthPatch WithEffect(this ISynthPatch patch, IEffect effect)
    {
        foreach (var p in patch.GetAllLeafPatches())
            if (p is SynthPatch s)
                s.Effects.Items.Add(effect.Clone());
        return patch;
    }

    public static ISynthPatch WithCabinet(this ISynthPatch patch)
        => patch.WithEffect(CabinetEffect.Create());

    public static ISynthPatch WithReverb(this ISynthPatch patch, float feedback = 0.78f, float diffusion = 0.5f, float wet = 0.3f, float dry = 0.7f)
        => patch.WithEffect(ReverbEffect.Create(feedback, diffusion, wet, dry));

    public static ISynthPatch WithDelay(this ISynthPatch patch, int delaySamples, float feedback = .3f, float mix = .4f)
        => patch.WithEffect(DelayEffect.Create(delaySamples, feedback, mix));

    public static ISynthPatch WithChorus(this ISynthPatch patch, int delayMs = 22, int depthMs = 7, float rateHz = 0.22f, float mix = 0.19f)
        => patch.WithEffect(StereoChorusEffect.Create(delayMs, depthMs, rateHz, mix));

    public static ISynthPatch WithTremolo(this ISynthPatch patch, float depth = 0.5f, float rateHz = 5f)
        => patch.WithEffect(TremoloEffect.Create(depth, rateHz));

    public static ISynthPatch WithHighPass(this ISynthPatch patch, float cutoffHz = 200f)
        => patch.WithEffect(HighPassFilterEffect.Create(cutoffHz));

    public static ISynthPatch WithLowPass(this ISynthPatch patch, float alpha)
        => patch.WithEffect(LowPassFilterEffect.Create(alpha));

    public static ISynthPatch WithDistortion(this ISynthPatch patch, float drive = 6f, float stageRatio = 0.6f, float bias = 0.15f)
        => patch.WithEffect(DistortionEffect.Create(drive, stageRatio, bias));

    public static ISynthPatch WithAggroDistortion(this ISynthPatch patch, float drive = 12f, float stageRatio = 0.8f, float bias = 0.12f)
        => patch.WithEffect(AggroDistortionEffect.Create(drive, stageRatio, bias));

    public static ISynthPatch WithVolume(this ISynthPatch patch, float volume = 1.0f)
        => patch.WithEffect(VolumeEffect.Create(volume));

    public static ISynthPatch WithPitchBend(this ISynthPatch patch, Func<float, float> bendFunc, float duration)
        => patch.WithEffect(PitchBendEffect.Create(bendFunc, duration));

    public static ISynthPatch WithNoiseGate(this ISynthPatch patch, float openThresh = 0.05f, float closeThresh = 0.04f, float attackMs = 2f, float releaseMs = 60f)
        => patch.WithEffect(NoiseGateEffect.Create(openThresh, closeThresh, attackMs, releaseMs));

    public static ISynthPatch WithFadeIn(this ISynthPatch patch, float durationSeconds = 1.5f)
        => patch.WithEffect(FadeInEffect.Create(durationSeconds));

    public static ISynthPatch WithFadeOut(this ISynthPatch patch, float durationSeconds = 1.5f)
        => patch.WithEffect(FadeOutEffect.Create(durationSeconds));

    public static ISynthPatch WithEnvelope(this ISynthPatch patch, double attackMs, double decayMs, double sustainLevel, double releaseMs)
        => patch.WithEffect(EnvelopeEffect.Create(attackMs, decayMs, sustainLevel, releaseMs));

    public static ISynthPatch WithToneStack(this ISynthPatch patch, float bass = 1f, float mid = 1f, float treble = 1f)
        => patch.WithEffect(ToneStackEffect.Create(bass, mid, treble));

    public static ISynthPatch WithPresenceShelf(this ISynthPatch patch, float presenceDb = +3f)
        => patch.WithEffect(PresenceShelfEffect.Create(presenceDb));

    public static ISynthPatch WithPickTransient(this ISynthPatch patch, float dur = .005f, float gain = .6f)
        => patch.WithEffect(PickTransientEffect.Create(dur, gain));

    public static ISynthPatch WithDCBlocker(this ISynthPatch patch)
        => patch.WithEffect(DCBlockerEffect.Create());

    public static ISynthPatch WithPeakEQ(this ISynthPatch patch, float freq, float gainDb, float q = 1.0f)
        => patch.WithEffect(ParametricEQEffect.Create(BiquadType.Peak, freq, gainDb, q));

    public static ISynthPatch WithLowShelf(this ISynthPatch patch, float freq, float gainDb)
        => patch.WithEffect(ParametricEQEffect.Create(BiquadType.LowShelf, freq, gainDb));

    public static ISynthPatch WithHighShelf(this ISynthPatch patch, float freq, float gainDb)
        => patch.WithEffect(ParametricEQEffect.Create(BiquadType.HighShelf, freq, gainDb));

    public static ISynthPatch WithPingPongDelay(this ISynthPatch patch, float delayMs = 330f, float feedback = 0.45f, float mix = 0.36f)
    {
        int delaySamples = (int)(delayMs * SoundProvider.SampleRate / 1000.0);
        return patch.WithEffect(PingPongDelayEffect.Create(delaySamples, feedback, mix));
    }

    // ----- Property: applies to all leaves -----

    public static ISynthPatch WithTransient(this ISynthPatch patch, float transientDurationSeconds = .01f)
    {
        foreach (var p in patch.GetAllLeafPatches())
            if (p is SynthPatch s)
            {
                s.EnableTransient = true;
                s.TransientDurationSeconds = transientDurationSeconds;
            }
        return patch;
    }

    public static ISynthPatch WithPitchDrift(this ISynthPatch patch, float driftFrequencyHz = 0.5f, float driftAmountCents = 5f)
    {
        foreach (var p in patch.GetAllLeafPatches())
            if (p is SynthPatch s)
            {
                s.EnablePitchDrift = true;
                s.DriftFrequencyHz = driftFrequencyHz;
                s.DriftAmountCents = driftAmountCents;
            }
        return patch;
    }

    public static ISynthPatch WithVibrato(this ISynthPatch patch, float rateHz = 5.8f, float depthCents = 35f, float phaseOffset = 0f)
    {
        foreach (var p in patch.GetAllLeafPatches())
            if (p is SynthPatch s)
            {
                s.EnableVibrato = true;
                s.VibratoRateHz = rateHz;
                s.VibratoDepthCents = depthCents;
                s.VibratoPhaseOffset = phaseOffset;
            }
        return patch;
    }

    public static ISynthPatch WithSubOscillator(this ISynthPatch patch, float subOscLevel = .5f, int subOscOctaveOffset = -1)
    {
        foreach (var p in patch.GetAllLeafPatches())
            if (p is SynthPatch s)
            {
                s.SubOscLevel = subOscLevel;
                s.SubOscOctaveOffset = subOscOctaveOffset;
            }
        return patch;
    }

    public static ISynthPatch WithWaveForm(this ISynthPatch patch, WaveformType waveform)
    {
        foreach (var p in patch.GetAllLeafPatches())
            if (p is SynthPatch s)
                s.Waveform = waveform;
        return patch;
    }

    // ----- Utility -----

    public static IEnumerable<ISynthPatch> GetAllLeafPatches(this ISynthPatch patch)
    {
        if (patch is ICompositePatch composite)
        {
            foreach (var child in composite.Patches)
                foreach (var leaf in child.GetAllLeafPatches())
                    yield return leaf;
        }
        else
        {
            yield return patch;
        }
    }

    public static SynthPatch? GetLeafSynthPatch(this ISynthPatch patch)
    {
        // Returns the *first* leaf SynthPatch, or null if none found
        if (patch is SynthPatch s)
            return s;
        if (patch is ICompositePatch composite)
        {
            foreach (var child in composite.Patches)
            {
                var leaf = child.GetLeafSynthPatch();
                if (leaf != null)
                    return leaf;
            }
        }
        return null;
    }

    // ----- Effect discovery -----

    public static EnvelopeEffect? FindEnvelopeEffect(this ISynthPatch patch)
    {
        foreach (var p in patch.GetAllLeafPatches())
        {
            if (p is SynthPatch s && s.Effects.Items != null)
            {
                for (var i = 0; i < s.Effects.Items.Count; i++)
                {
                    if (s.Effects.Items[i] is EnvelopeEffect env)
                        return env;
                }
            }
        }
        return null;
    }
}


public interface IEffect
{
    // Process a mono sample (or stereo, if you want!)
    float Process(float input, int frameIndex, float time);
    IEffect Clone();
}

