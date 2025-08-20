using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class SynthPatchExtensions
{
    // ----- Effect: applies to all leaves -----

    [ExtensionToEffect(typeof(CabinetEffect))]
    public static ISynthPatch WithCabinet(this ISynthPatch patch)
    {
        var settings = new CabinetEffect.Settings { VelocityScale = 1f };
        return patch.WithEffect(CabinetEffect.Create(in settings));
    }

    public static ISynthPatch WithReverb(
        this ISynthPatch patch,
        float feedback = 0.78f,            // Decay length: 0.7-0.85 = medium-long tail
        float diffusion = 0.65f,           // Reflection density: 0.5–0.8 = smooth
        float wet = 0.27f,                 // Wet level
        float dry = 0.73f,                 // Dry level
        float damping = 0.45f,             // High-freq tail rolloff (0.2–0.7 typical)
        float duration = .2f,
        float inputLowpassHz = 9500f,      // Pre-reverb hi-cut (7000–12000 typical)
        bool velocityAffectsMix = true,    // Velocity-sensitive reverb amount
        Func<float, float>? mixVelocityCurve = null, // Curve for velocity mix, null=linear
        bool enableModulation = true       // Enable comb LFO modulation for richer tail (false=lower cpu)
    )
    {
        var effectSettings = new ReverbEffect.Settings
        {
            Feedback = feedback,
            Diffusion = diffusion,
            Damping = damping,
            Wet = wet,
            Dry = dry,
            InputLowpassHz = inputLowpassHz,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve,
            EnableModulation = enableModulation
        };
        var patchSettings = new ReverbPatch.Settings
        {
            BasePatch = patch,
            Duration = duration,
            EffectSettings = effectSettings
        };
        return ReverbPatch.Create(patchSettings);
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
            VelocityAffectsMix = false
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
            VelocityAffectsMix = false
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
            VelocityAffectsMix = false
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
            VelocityAffectsMix = false
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

    [ExtensionToEffect(typeof(PortamentoEffect))]
    public static ISynthPatch WithPortamento(this ISynthPatch patch)
    {
        return patch.WithEffect(PortamentoEffect.Create());
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
    public static ISynthPatch WithEnvelope(this ISynthPatch patch, double attack, double decay, double sustainLevel, double release)
    {
        var settings = new EnvelopeEffect.Settings
        {
            Attack = attack,
            Decay = decay,
            Sustain = sustainLevel,
            Release = release
        };
        return patch.WithEffect(EnvelopeEffect.Create(in settings));
    }


    [ExtensionToEffect(typeof(EnvelopeEffect))]
    public static ISynthPatch WithEnvelope(this ISynthPatch patch, double delay, Func<double,double?> attack, Func<double, double?> decay, Func<double, double> sustainLevel, Func<double, double?> release)
    {
        var settings = new CurvedEnvelopeEffect.Settings
        {
            Delay = delay,
            Attack = attack,
            Decay = decay,
            Sustain = sustainLevel,
            Release = release
        };
        return patch.WithEffect(CurvedEnvelopeEffect.Create(in settings));
    }

    public static ISynthPatch WithEnvelope(this ISynthPatch patch, double delay, double attack, double decay, double sustainLevel, double release)
    {
        var settings = new EnvelopeEffect.Settings
        {
            Delay = delay,
            Attack = attack,
            Decay = decay,
            Sustain = sustainLevel,
            Release = release
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
    public static ISynthPatch WithPresenceShelf(this ISynthPatch patch, PresenceSettings settings)
    {
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
        patch.ForEachLeafPatch(p =>
        {
            if (p is SynthPatch s)
                s.Effects.Items.Add(effect.Clone());
        });
        return patch;
    }

    [CoreEffect]
    public static ISynthPatch WithTransient(this ISynthPatch patch, float transientDurationSeconds = .01f)
    {
        patch.ForEachLeafPatch(p =>
        {
            if (p is SynthPatch s)
            {
                s.EnableTransient = true;
                s.TransientDurationSeconds = transientDurationSeconds;
            }
        });
        return patch;
    }

    [CoreEffect]
    [SynthDocumentation("""
        Sets a fixed MIDI note override for all leaf patches.
        This allows you to force the patch to always play a specific note, regardless of the input note.
        """)]
    public static ISynthPatch WithMidiOverride(this ISynthPatch patch, int midiNote)
    {
        var frequency = NoteExpression.MidiNoteToFrequency(midiNote);
        return WithFrequencyOverride(patch, frequency);
    }

    [CoreEffect]
    [SynthDocumentation("""
    Sets a fixed frequency override for all leaf patches.
    """)]
    public static ISynthPatch WithFrequencyOverride(this ISynthPatch patch, float frequency)
    {
        patch.ForEachLeafPatch(p =>
        {
            if (p is SynthPatch s)
                s.FrequencyOverride = frequency;
        });
        return patch;
    }

    [CoreEffect]
    public static ISynthPatch WithPitchDrift(this ISynthPatch patch,
        [SynthDocumentation("""
        The speed of the pitch drift in Hertz (Hz).  
        This sets how quickly the pitch wobbles up and down, similar to subtle analog oscillator instability or "wow and flutter."  
        Typical values range from 0.1 Hz (very slow, barely noticeable) to 2 Hz (more pronounced, warbly effect).  
        Lower values give a gentle, organic feel; higher values sound more dramatic.
        """)] float driftFrequencyHz = 0.5f,

        [SynthDocumentation("""
        The depth of the pitch drift in cents (1/100th of a semitone).  
        This controls how far the pitch moves above and below the note’s true pitch.  
        Small values (2–10 cents) mimic classic analog synth imperfections; larger values will sound more unstable or detuned.
        """)] float driftAmountCents = 5f)
    {
        patch.ForEachLeafPatch(p =>
        {
            if (p is SynthPatch s)
            {
                s.EnablePitchDrift = true;
                s.DriftFrequencyHz = driftFrequencyHz;
                s.DriftAmountCents = driftAmountCents;
            }
        });
        return patch;
    }


    [CoreEffect]
    public static ISynthPatch WithSubOscillator(
        this ISynthPatch patch,
        [SynthDocumentation("""
        The level (volume) of the sub-oscillator as a fraction of the main oscillator (0–1).  
        Higher values blend more of the deep sub-bass tone into the sound.  
        Typical values are 0.3–0.7 for a noticeable bass boost, or set to 0 to disable.
        """)] float subOscLevel = .5f,

        [SynthDocumentation("""
        The pitch offset for the sub-oscillator, in octaves.  
        Negative values (like -1) shift the sub-oscillator down one octave for classic fat bass.  
        0 keeps it at the same pitch as the main oscillator, while +1 raises it one octave.
        """)] int subOscOctaveOffset = -1)
    {
        patch.ForEachLeafPatch(p =>
        {
            if (p is SynthPatch s)
            {
                s.SubOscLevel = subOscLevel;
                s.SubOscOctaveOffset = subOscOctaveOffset;
            }
        });
        return patch;
    }

    

    [CoreEffect]
    public static ISynthPatch WithWaveForm(
        this ISynthPatch patch,
        [SynthDocumentation("""
        The type of oscillator waveform to use.  
        Common types:  
        - Sine: Smooth, pure tone  
        - Triangle: Soft, flute-like  
        - Saw: Bright, buzzy, good for synth leads  
        - Square: Hollow, woody, good for basses  
        - PluckedString: Physical model for guitar/bass/plucked instruments  
        Each waveform has its own overtone spectrum and character.
        """)] WaveformType waveform)
    {
        patch.ForEachLeafPatch(p =>
        {
            if (p is SynthPatch s)
                s.Waveform = waveform;
        });
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

    public static void ForEachLeafPatch(this ISynthPatch patch, Action<ISynthPatch> action)
    {
        if (patch is ICompositePatch composite)
        {
            foreach (var child in composite.GetPatches())
            {
                child.ForEachLeafPatch(action);
            }
        }
        else
        {
            action(patch);
        }
    }

 
    public static IEnvelope? FindEnvelope(this ISynthPatch patch)
    {
        IEnvelope ret = null;
        patch.ForEachLeafPatch(leaf =>
        {
            if (leaf is SynthPatch s && s.Effects.Items != null)
            {
                for (int j = 0; j < s.Effects.Items.Count; j++)
                {
                    if (s.Effects.Items[j] is EnvelopeEffect env)
                    {
                        ret = env.Envelope;
                        return;
                    }
                    if (s.Effects.Items[j] is CurvedEnvelopeEffect env2)
                    {
                        ret = env2.Envelope;
                        return;
                    }
                }
            }
        });

        return ret;
    }
}
