using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public interface ICompositePatch : ISynthPatch 
{
    void GetPatches(List<ISynthPatch> buffer);
}

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
    void SpawnVoices(float frequencyHz, VolumeKnob master, NoteExpression note, List<SynthSignalSource> outVoices);
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

    public RecyclableList<IEffect> Effects { get; set; }

    protected override void OnInit()
    {
        base.OnInit();
        Effects = RecyclableListPool<IEffect>.Instance.Rent(20);
        Waveform = WaveformType.Sine;
        EnableTransient = false;
        TransientDurationSeconds = 0f;
        EnableSubOsc = false;
        SubOscLevel = 0f;
        SubOscOctaveOffset = 0;
        EnablePitchDrift = false;
        DriftFrequencyHz = 0f;
        DriftAmountCents = 0f;
        Velocity = 127;
        EnableVibrato = false;
        VibratoRateHz = 0f;
        VibratoDepthCents = 0f;
        VibratoPhaseOffset = 0f;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        for (var i = 0; i < Effects?.Count; i++)
        {
            if (Effects[i] is Recyclable r) r.TryDispose();
        }
        Effects.Dispose();
        Effects = null!;
    }

    public virtual void SpawnVoices(float frequencyHz, VolumeKnob master, NoteExpression note, List<SynthSignalSource> outVoices)
    {
        var innerVoice = SynthSignalSource.Create(frequencyHz, this, master, note);
        this.OnDisposed(innerVoice, Recyclable.TryDisposeMe);
        outVoices.Add(innerVoice);
    }
}

public static class SynthPatchExtensions
{
    // ----- Effect: applies to all leaves -----



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

    public static ISynthPatch WithLowPass(this ISynthPatch patch, float cutoffHz = 200f)
        => patch.WithEffect(LowPassFilterEffect.Create(cutoffHz));

    public static ISynthPatch WithDistortion(this ISynthPatch patch, float drive = 6f, float stageRatio = 0.6f, float bias = 0.15f)
        => patch.WithEffect(DistortionEffect.Create(drive, stageRatio, bias));

    public static ISynthPatch WithAggroDistortion(this ISynthPatch patch, float drive = 12f, float stageRatio = 0.8f, float bias = 0.12f)
        => patch.WithEffect(AggroDistortionEffect.Create(drive, stageRatio, bias));

    public static ISynthPatch WithVolume(this ISynthPatch patch, float volume = 1.0f)
        => patch.WithEffect(VolumeEffect.Create(volume));

    public static ISynthPatch WithPitchBend(
        this ISynthPatch patch,
        Func<float, float> attackBendFunc, float attackDuration,
        Func<float, float> releaseBendFunc, float releaseDuration)
        => patch.WithEffect(PitchBendEffect.Create(attackBendFunc, attackDuration, releaseBendFunc, releaseDuration));

    public static ISynthPatch WithPitchBend(
    this ISynthPatch patch,
    Func<float, float> bendFunc, float duration)
    => patch.WithEffect(PitchBendEffect.Create(
        attackBend: bendFunc, attackDur: duration,
        releaseBend: t => 0f, releaseDur: 0.001f)); // Release bend = no bend

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

    public static ISynthPatch WithEffect(this ISynthPatch patch, IEffect effect)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
                if (leaves.Items[i] is SynthPatch s)
                    s.Effects.Items.Add(effect.Clone());
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }

    public static ISynthPatch WithTransient(this ISynthPatch patch, float transientDurationSeconds = .01f)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
                if (leaves.Items[i] is SynthPatch s)
                {
                    s.EnableTransient = true;
                    s.TransientDurationSeconds = transientDurationSeconds;
                }
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }

    public static ISynthPatch WithPitchDrift(this ISynthPatch patch, float driftFrequencyHz = 0.5f, float driftAmountCents = 5f)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
                if (leaves.Items[i] is SynthPatch s)
                {
                    s.EnablePitchDrift = true;
                    s.DriftFrequencyHz = driftFrequencyHz;
                    s.DriftAmountCents = driftAmountCents;
                }
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }

    public static ISynthPatch WithVibrato(this ISynthPatch patch, float rateHz = 5.8f, float depthCents = 35f, float phaseOffset = 0f)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
                if (leaves.Items[i] is SynthPatch s)
                {
                    s.EnableVibrato = true;
                    s.VibratoRateHz = rateHz;
                    s.VibratoDepthCents = depthCents;
                    s.VibratoPhaseOffset = phaseOffset;
                }
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }

    public static ISynthPatch WithSubOscillator(this ISynthPatch patch, float subOscLevel = .5f, int subOscOctaveOffset = -1)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
                if (leaves.Items[i] is SynthPatch s)
                {
                    s.SubOscLevel = subOscLevel;
                    s.SubOscOctaveOffset = subOscOctaveOffset;
                }
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }

    public static ISynthPatch WithWaveForm(this ISynthPatch patch, WaveformType waveform)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
                if (leaves.Items[i] is SynthPatch s)
                    s.Waveform = waveform;
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }


    // ----- Utility -----

    public static void GetAllLeafPatches(this ISynthPatch patch, RecyclableList<ISynthPatch>? outputBuffer = null)
    {
        bool isRoot = false;
        if (outputBuffer == null)
        {
            isRoot = true;
            outputBuffer = RecyclableListPool<ISynthPatch>.Instance.Rent(20);
        }

        if (patch is ICompositePatch composite)
        {
            var children = RecyclableListPool<ISynthPatch>.Instance.Rent(8);
            try
            {
                composite.GetPatches(children.Items);
                for (int i = 0; i < children.Items.Count; i++)
                {
                    children.Items[i].GetAllLeafPatches(outputBuffer);
                }
            }
            finally
            {
                children.Dispose();
            }
        }
        else
        {
            outputBuffer.Items.Add(patch);
        }

        if (isRoot)
        {
            // outputBuffer now contains only leaves
            outputBuffer.Dispose();
        }
    }

    public static SynthPatch? GetLeafSynthPatch(this ISynthPatch patch)
    {
        if (patch is SynthPatch s)
            return s;

        if (patch is ICompositePatch composite)
        {
            var children = RecyclableListPool<ISynthPatch>.Instance.Rent(8);
            try
            {
                composite.GetPatches(children.Items);

                for (int i = 0; i < children.Items.Count; i++)
                {
                    var leaf = children.Items[i].GetLeafSynthPatch();
                    if (leaf != null)
                    {
                        return leaf;
                    }
                }
            }
            finally
            {
                children.Dispose();
            }
        }

        return null;
    }



    private static RecyclableList<ISynthPatch> envelopeBuffer = RecyclableListPool<ISynthPatch>.Instance.Rent(16);

    public static EnvelopeEffect? FindEnvelopeEffect(this ISynthPatch patch)
    {
        envelopeBuffer.Items.Clear();
        patch.GetAllLeafPatches(envelopeBuffer);

        for (int i = 0; i < envelopeBuffer.Items.Count; i++)
        {
            if (envelopeBuffer.Items[i] is SynthPatch s && s.Effects.Items != null)
            {
                for (int j = 0; j < s.Effects.Items.Count; j++)
                {
                    if (s.Effects.Items[j] is EnvelopeEffect env)
                    {
                        return env;
                    }
                }
            }
        }

        return null;
    }

}


public interface IEffect
{
    // Process a mono sample (or stereo, if you want!)
    float Process(in EffectContext ctx);
    IEffect Clone();
}

