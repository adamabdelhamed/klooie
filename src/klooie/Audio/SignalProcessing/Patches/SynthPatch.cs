using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static klooie.SynthSignalSource;

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

[SynthCategory("Core")]
[SynthDescription("""
Single-oscillator patch that forms the basis of most instruments.  Optional
components include a sub oscillator, vibrato, gentle pitch drift and a
pluggable effects chain.  Use this as a starting point for custom patches.
""")]
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

    public List<LfoSettings> Lfos { get; } = new();

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
        Lfos.Clear();
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


    [ExtensionToEffect(typeof(CabinetEffect))]
    public static ISynthPatch WithCabinet(this ISynthPatch patch)
    {
        var settings = new CabinetEffect.Settings { VelocityScale = 1f };
        return patch.WithEffect(CabinetEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ReverbEffect))]
    public static ISynthPatch WithReverb(this ISynthPatch patch, float feedback = 0.78f, float diffusion = 0.5f, float wet = 0.3f, float dry = 0.7f)
    {
        var settings = new ReverbEffect.Settings
        {
            Feedback = feedback,
            Diffusion = diffusion,
            Wet = wet,
            Dry = dry,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(ReverbEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(DelayEffect))]
    public static ISynthPatch WithDelay(this ISynthPatch patch, int delaySamples, float feedback = .3f, float mix = .4f)
    {
        var settings = new DelayEffect.Settings
        {
            DelaySamples = delaySamples,
            Feedback = feedback,
            Mix = mix,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(DelayEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(StereoChorusEffect))]
    public static ISynthPatch WithChorus(this ISynthPatch patch, int delayMs = 22, int depthMs = 7, float rateHz = 0.22f, float mix = 0.19f)
    {
        var settings = new StereoChorusEffect.Settings
        {
            DelayMs = delayMs,
            DepthMs = depthMs,
            RateHz = rateHz,
            Mix = mix,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(StereoChorusEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(TremoloEffect))]
    public static ISynthPatch WithTremolo(this ISynthPatch patch, float depth = 0.5f, float rateHz = 5f)
    {
        var settings = new TremoloEffect.Settings
        {
            Depth = depth,
            RateHz = rateHz,
            VelocityAffectsDepth = true
        };
        return patch.WithEffect(TremoloEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(HighPassFilterEffect))]
    public static ISynthPatch WithHighPass(this ISynthPatch patch, float cutoffHz = 200f)
    {
        var settings = new HighPassFilterEffect.Settings
        {
            CutoffHz = cutoffHz,
            Mix = 1f,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(HighPassFilterEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(HighPassFilterEffect))]
    public static ISynthPatch WithHighPassRelative(this ISynthPatch patch, float multiplier = 2f)
    {
        var settings = new HighPassFilterEffect.Settings
        {
            NoteFrequencyMultiplier = multiplier,
            Mix = 1f,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(HighPassFilterEffect.Create(in settings));
    }
    
    [ExtensionToEffect(typeof(LowPassFilterEffect))]
    public static ISynthPatch WithLowPass(this ISynthPatch patch, float cutoffHz = 200f)
    {
        var settings = new LowPassFilterEffect.Settings
        {
            CutoffHz = cutoffHz,
            Mix = 1f,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(LowPassFilterEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(LowPassFilterEffect))]
    public static ISynthPatch WithLowPassRelative(this ISynthPatch patch, float multiplier = 1f)
    {
        var settings = new LowPassFilterEffect.Settings
        {
            NoteFrequencyMultiplier = multiplier,
            Mix = 1f,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(LowPassFilterEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(DistortionEffect))]
    public static ISynthPatch WithDistortion(this ISynthPatch patch, float drive = 6f, float stageRatio = 0.6f, float bias = 0.15f)
    {
        var settings = new DistortionEffect.Settings
        {
            Drive = drive,
            StageRatio = stageRatio,
            Bias = bias,
            VelocityScale = 1f
        };
        return patch.WithEffect(DistortionEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(AggroDistortionEffect))]
    public static ISynthPatch WithAggroDistortion(this ISynthPatch patch, float drive = 12f, float stageRatio = 0.8f, float bias = 0.12f)
    {
        var settings = new AggroDistortionEffect.Settings
        {
            Drive = drive,
            StageRatio = stageRatio,
            Bias = bias,
            VelocityScale = 1f
        };
        return patch.WithEffect(AggroDistortionEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(VolumeEffect))]
    public static ISynthPatch WithVolume(this ISynthPatch patch, float volume = 1.0f)
    {
        var settings = new VolumeEffect.Settings { Gain = volume, VelocityScale = 1f };
        return patch.WithEffect(VolumeEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(PitchBendEffect))]
    public static ISynthPatch WithPitchBend(
        this ISynthPatch patch,
        Func<float, float> attackBendFunc, float attackDuration,
        Func<float, float> releaseBendFunc, float releaseDuration)
    {
        var settings = new PitchBendEffect.Settings
        {
            AttackBend = attackBendFunc,
            AttackDuration = attackDuration,
            ReleaseBend = releaseBendFunc,
            ReleaseDuration = releaseDuration
        };
        return patch.WithEffect(PitchBendEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(PitchBendEffect))]
    public static ISynthPatch WithPitchBend(
    this ISynthPatch patch,
    Func<float, float> bendFunc, float duration)
    {
        var settings = new PitchBendEffect.Settings
        {
            AttackBend = bendFunc,
            AttackDuration = duration,
            ReleaseBend = t => 0f,
            ReleaseDuration = 0.001f
        };
        return patch.WithEffect(PitchBendEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(NoiseGateEffect))]
    public static ISynthPatch WithNoiseGate(this ISynthPatch patch, float openThresh = 0.05f, float closeThresh = 0.04f, float attackMs = 2f, float releaseMs = 60f)
    {
        var settings = new NoiseGateEffect.Settings
        {
            OpenThresh = openThresh,
            CloseThresh = closeThresh,
            AttackMs = attackMs,
            ReleaseMs = releaseMs,
            VelocityAffectsThreshold = true
        };
        return patch.WithEffect(NoiseGateEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(FadeInEffect))]
    public static ISynthPatch WithFadeIn(this ISynthPatch patch, float durationSeconds = 1.5f)
    {
        var settings = new FadeInEffect.Settings
        {
            DurationSeconds = durationSeconds,
            VelocityScale = 1f
        };
        return patch.WithEffect(FadeInEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(FadeOutEffect))]
    public static ISynthPatch WithFadeOut(this ISynthPatch patch, float durationSeconds = 1.5f)
    {
        var settings = new FadeOutEffect.Settings
        {
            DurationSeconds = durationSeconds,
            FadeStartTime = 0f,
            VelocityScale = 1f
        };
        return patch.WithEffect(FadeOutEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(EnvelopeEffect))]
    public static ISynthPatch WithEnvelope(this ISynthPatch patch, double attackMs, double decayMs, double sustainLevel, double releaseMs)
    {
        var settings = new EnvelopeEffect.Settings
        {
            Attack = attackMs,
            Decay = decayMs,
            Sustain = sustainLevel,
            Release = releaseMs
        };
        return patch.WithEffect(EnvelopeEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ToneStackEffect))]
    public static ISynthPatch WithToneStack(this ISynthPatch patch, float bass = 1f, float mid = 1f, float treble = 1f)
    {
        var settings = new ToneStackEffect.Settings
        {
            Bass = bass,
            Mid = mid,
            Treble = treble,
            VelocityAffectsGain = true
        };
        return patch.WithEffect(ToneStackEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(PresenceShelfEffect))]
    public static ISynthPatch WithPresenceShelf(this ISynthPatch patch, float presenceDb = +3f)
    {
        var settings = new PresenceShelfEffect.Settings
        {
            PresenceDb = presenceDb,
            VelocityScale = 1f
        };
        return patch.WithEffect(PresenceShelfEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(PickTransientEffect))]
    public static ISynthPatch WithPickTransient(this ISynthPatch patch, float dur = .005f, float gain = .6f)
    {
        var settings = new PickTransientEffect.Settings { Duration = dur, Gain = gain };
        return patch.WithEffect(PickTransientEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(DCBlockerEffect))]
    public static ISynthPatch WithDCBlocker(this ISynthPatch patch)
    {
        var settings = new DCBlockerEffect.Settings();
        return patch.WithEffect(DCBlockerEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ParametricEQEffect))]
    public static ISynthPatch WithPeakEQ(this ISynthPatch patch, float freq, float gainDb, float q = 1.0f)
    {
        var settings = new ParametricEQEffect.Settings
        {
            Type = BiquadType.Peak,
            Freq = freq,
            GainDb = gainDb,
            Q = q,
            VelocityAffectsGain = true,
            GainVelocityScale = 1f
        };
        return patch.WithEffect(ParametricEQEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ParametricEQEffect))]
    public static ISynthPatch WithPeakEQRelative(this ISynthPatch patch, float multiplier, float gainDb, float q = 1.0f)
    {
        var settings = new ParametricEQEffect.Settings
        {
            Type = BiquadType.Peak,
            NoteFrequencyMultiplier = multiplier,
            GainDb = gainDb,
            Q = q,
            VelocityAffectsGain = true,
            GainVelocityScale = 1f
        };
        return patch.WithEffect(ParametricEQEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ParametricEQEffect))]
    public static ISynthPatch WithLowShelf(this ISynthPatch patch, float freq, float gainDb)
    {
        var settings = new ParametricEQEffect.Settings
        {
            Type = BiquadType.LowShelf,
            Freq = freq,
            GainDb = gainDb,
            Q = 1f,
            VelocityAffectsGain = true,
            GainVelocityScale = 1f
        };
        return patch.WithEffect(ParametricEQEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ParametricEQEffect))]
    public static ISynthPatch WithLowShelfRelative(this ISynthPatch patch, float multiplier, float gainDb)
    {
        var settings = new ParametricEQEffect.Settings
        {
            Type = BiquadType.LowShelf,
            NoteFrequencyMultiplier = multiplier,
            GainDb = gainDb,
            Q = 1f,
            VelocityAffectsGain = true,
            GainVelocityScale = 1f
        };
        return patch.WithEffect(ParametricEQEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ParametricEQEffect))]
    public static ISynthPatch WithHighShelf(this ISynthPatch patch, float freq, float gainDb)
    {
        var settings = new ParametricEQEffect.Settings
        {
            Type = BiquadType.HighShelf,
            Freq = freq,
            GainDb = gainDb,
            Q = 1f,
            VelocityAffectsGain = true,
            GainVelocityScale = 1f
        };
        return patch.WithEffect(ParametricEQEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(ParametricEQEffect))]
    public static ISynthPatch WithHighShelfRelative(this ISynthPatch patch, float multiplier, float gainDb)
    {
        var settings = new ParametricEQEffect.Settings
        {
            Type = BiquadType.HighShelf,
            NoteFrequencyMultiplier = multiplier,
            GainDb = gainDb,
            Q = 1f,
            VelocityAffectsGain = true,
            GainVelocityScale = 1f
        };
        return patch.WithEffect(ParametricEQEffect.Create(in settings));
    }

    [ExtensionToEffect(typeof(PingPongDelayEffect))]
    public static ISynthPatch WithPingPongDelay(this ISynthPatch patch, float delayMs = 330f, float feedback = 0.45f, float mix = 0.36f)
    {
        int delaySamples = (int)(delayMs * SoundProvider.SampleRate / 1000.0);
        var settings = new PingPongDelayEffect.Settings
        {
            DelaySamples = delaySamples,
            Feedback = feedback,
            Mix = mix,
            VelocityAffectsMix = true
        };
        return patch.WithEffect(PingPongDelayEffect.Create(in settings));
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

    [CoreEffect]
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

    [CoreEffect]
    public static ISynthPatch WithPitchDrift(this ISynthPatch patch,
    [SynthDescription("""
    The speed of the pitch drift in Hertz (Hz).  
    This sets how quickly the pitch wobbles up and down, similar to subtle analog oscillator instability or "wow and flutter."  
    Typical values range from 0.1 Hz (very slow, barely noticeable) to 2 Hz (more pronounced, warbly effect).  
    Lower values give a gentle, organic feel; higher values sound more dramatic.
    """)] float driftFrequencyHz = 0.5f,

    [SynthDescription("""
    The depth of the pitch drift in cents (1/100th of a semitone).  
    This controls how far the pitch moves above and below the note’s true pitch.  
    Small values (2–10 cents) mimic classic analog synth imperfections; larger values will sound more unstable or detuned.
    """)] float driftAmountCents = 5f)
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

    [CoreEffect]
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

    [CoreEffect]
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

    [CoreEffect]
    public static ISynthPatch WithLFO(
        this ISynthPatch patch,
        SynthSignalSource.LfoTarget target,
        float rateHz,
        float depth,
        int shape = 0,
        float phaseOffset = 0f,
        bool velocityAffectsDepth = false,
        Func<float, float>? depthVelocityCurve = null)
    {
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(16);
        try
        {
            patch.GetAllLeafPatches(leaves);
            for (int i = 0; i < leaves.Items.Count; i++)
            {
                if (leaves.Items[i] is SynthPatch s)
                {
                    var lfo = new SynthSignalSource.LfoSettings
                    {
                        Target = target,
                        RateHz = rateHz,
                        Depth = depth,
                        Shape = shape,
                        PhaseOffset = phaseOffset,
                        VelocityAffectsDepth = velocityAffectsDepth,
                        DepthVelocityCurve = depthVelocityCurve
                    };
                    s.Lfos.Add(lfo);
                }
            }
        }
        finally
        {
            leaves.Dispose();
        }
        return patch;
    }


    [CoreEffect]
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


    [ExtensionToEffect(typeof(CompressorEffect))]
    public static ISynthPatch WithCompressor(this ISynthPatch patch, float threshold = 0.5f, float ratio = 4f, float attackMs = 0.01f, float releaseMs = 0.05f)
    {
        var settings = new CompressorEffect.Settings
        {
            Threshold = threshold,
            Ratio = ratio,
            Attack = attackMs,
            Release = releaseMs,
            VelocityScale = 1f
        };
        return patch.WithEffect(CompressorEffect.Create(in settings));
    }

    [ExtensionToPatch(typeof(UnisonPatch))]
    public static ISynthPatch WrapWithUnison(this ISynthPatch patch, int numVoices = 2, float detuneCents = 0, float panSpread = 1)
    {
        var ret = UnisonPatch.Create(new UnisonPatch.Settings
        {
            BasePatch = patch,
            NumVoices = numVoices,
            DetuneCents = detuneCents,
            PanSpread = panSpread
        });
        return ret;
    }

    [ExtensionToPatch(typeof(PowerChordPatch))]
    public static ISynthPatch WrapWithPowerChord(this ISynthPatch patch, int[] intervals, float detuneCents = 0, float panSpread = 1)
    {
        var ret = PowerChordPatch.Create(new PowerChordPatch.Settings
        {
            BasePatch = patch,
            Intervals = intervals,
            DetuneCents = detuneCents,
            PanSpread = panSpread
        });
        return ret;
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

