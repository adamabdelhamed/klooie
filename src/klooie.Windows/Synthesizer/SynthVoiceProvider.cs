using NAudio.Wave;

namespace klooie;

public enum WaveformType
{
    Sine,
    Square,
    Saw,
    Triangle,
    Noise,
    PluckedString,
}

public class SynthVoiceProvider : RecyclableAudioProvider
{
    private float frequency;
    private double durationSeconds;
    private WaveFormat waveFormat;
    private double sampleRate;
    private float time;
    private float filteredSample = 0f;
    private SynthPatch patch;

    private float driftPhase;
    private float driftPhaseIncrement;
    private float driftRandomOffset;
    private float[]? pluckBuffer;
    private int pluckLength = 0;
    private int pluckWriteIndex = 0;
    private bool isDone;
    private double initialPhase;  

    private static readonly LazyPool<SynthVoiceProvider> _pool = new(() => new SynthVoiceProvider());

    public static SynthVoiceProvider Create(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob master, VolumeKnob? sample)
    {
        var ret = _pool.Value.Rent();
        ret.frequency = frequencyHz;
        ret.durationSeconds = durationSeconds;
        ret.waveFormat = new WaveFormat(CachedSound.ExpectedSampleRate, CachedSound.ExpectedBitsPerSample, CachedSound.ExpectedChannels);
        ret.sampleRate = ret.waveFormat.SampleRate;
        ret.time = 0;
        ret.filteredSample = 0;
        ret.InitVolume(master, sample);
        ret.patch = patch;
        ret.isDone = false;
        ret.initialPhase = Random.Shared.NextDouble() * 2 * Math.PI;
        patch.Envelope.Trigger(0, ret.sampleRate);

        if (patch.EnablePitchDrift)
        {
            ret.driftPhase = 0;
            ret.driftPhaseIncrement = (float)(2 * Math.PI * patch.DriftFrequencyHz / ret.sampleRate);
            ret.driftRandomOffset = Random.Shared.NextSingle() * (2 * MathF.PI);
        }

        if (patch.Waveform == WaveformType.PluckedString)
        {
            int delaySamples = (int)(ret.waveFormat.SampleRate / frequencyHz);
            ret.pluckBuffer = new float[delaySamples];
            ret.pluckLength = delaySamples;
            ret.pluckWriteIndex = 0;

            for (int i = 0; i < delaySamples; i++)
            {
                ret.pluckBuffer[i] = (float)(Random.Shared.NextDouble() * 2.0 - 1.0);
            }
        }

        return ret;
    }

    public override WaveFormat WaveFormat => waveFormat;

    public void ReleaseNote() => patch.Envelope.ReleaseNote(time);

    public override int Read(float[] buffer, int offset, int count)
    {
        int samplesWritten = 0;

        for (int n = 0; n < count; n += 2)
        {
            float level = patch.Envelope.GetLevel(time);

            // Only mark done if level is *actually* silent for some time
            if (level <= 0.0001f && patch.Envelope.IsDone(time))
            {
                isDone = true;
                break;
            }

            float sample = Osc(time);

            if (patch.EnableSubOsc)
            {
                float subTime = time * (float)Math.Pow(2, patch.SubOscOctaveOffset);
                sample += Osc(subTime, overrideWave: WaveformType.Sine) * patch.SubOscLevel;
            }

            if (patch.EnableTransient && time < patch.TransientDurationSeconds)
            {
                sample += (float)(Random.Shared.NextDouble() * 2 - 1) * 0.3f;
            }

            if (patch.EnableLowPassFilter)
            {
                float dynamicFactor = patch.EnableDynamicFilter ? patch.Velocity : 1f;
                float alpha = patch.FilterBaseAlpha + dynamicFactor * (patch.FilterMaxAlpha - patch.FilterBaseAlpha);
                alpha = Math.Clamp(alpha, 0f, 1f);
                filteredSample += alpha * (sample - filteredSample);
                sample = filteredSample;
            }

            float finalSample = sample * level * effectiveVolume;

            if (patch.EnableDistortion)
            {
                float dynamicDistortion = patch.DistortionAmount * patch.Velocity;
                finalSample = ApplyDistortion(finalSample, dynamicDistortion);
            }

            finalSample = Math.Clamp(finalSample, -1f, 1f);
            buffer[offset + n] = finalSample;
            buffer[offset + n + 1] = finalSample;

            time += 1f / (float)sampleRate;
            samplesWritten += 2;
        }

        // Zero the rest of the buffer if not fully filled
        for (int i = offset + samplesWritten; i < offset + count; i++)
        {
            buffer[i] = 0f;
        }

        return count;
    }



    private float ApplyDistortion(float sample, float amount)
    {
        // amount: 0 = clean, 1 = full drive
        float drive = 1f + (10f - 1f) * amount;
        return MathF.Tanh(sample * drive);
    }



    private float Osc(float t, WaveformType? overrideWave = null)
    {
        float driftedFrequency = frequency;

        if (patch.EnablePitchDrift)
        {
            float cents = patch.DriftAmountCents * MathF.Sin(driftPhase + driftRandomOffset);
            float multiplier = MathF.Pow(2f, cents / 1200f); // cents to ratio
            driftedFrequency *= multiplier;
        }

        driftPhase += driftPhaseIncrement;

        double phase = 2 * Math.PI * driftedFrequency * t + initialPhase;
        var wave = overrideWave ?? patch.Waveform;

        return wave switch
        {
            WaveformType.Sine => (float)Math.Sin(phase),
            WaveformType.Square => MathF.Sign(MathF.Sin((float)phase)),
            WaveformType.Triangle => 2f * MathF.Abs(2f * (float)(t * frequency % 1f) - 1f) - 1f,
            WaveformType.Saw => 2f * ((float)(t * frequency % 1f)) - 1f,
            WaveformType.Noise => (float)(Random.Shared.NextDouble() * 2 - 1),
            WaveformType.PluckedString => GetPluckedSample(),
            _ => 0f
        };
    }

    private float GetPluckedSample()
    {
        if (pluckBuffer == null || pluckLength == 0)
            return 0;

        int nextIndex = (pluckWriteIndex + 1) % pluckLength;
        float current = pluckBuffer[pluckWriteIndex];
        float next = pluckBuffer[nextIndex];

        float damping = 0.98f; // lower = more damping, less buzz
        float newSample = damping * 0.5f * (current + next);

        pluckBuffer[pluckWriteIndex] = newSample;
        pluckWriteIndex = nextIndex;

        return current;
    }

    protected override void OnReturn()
    {
        time = 0;
        isDone = true; // critical: prevent use-after-return
        patch = null!;
        pluckBuffer = null;
        pluckLength = 0;
        pluckWriteIndex = 0;
        base.OnReturn();
    }
}

public class SynthPatch
{
    public WaveformType Waveform;
    public ADSREnvelope Envelope = new();
    public bool EnableTransient = false;
    public float TransientDurationSeconds = 0.01f;
    public bool EnableLowPassFilter = false;
    public float FilterAlpha = 0.05f;

    public bool EnableDynamicFilter = false;
    public float FilterBaseAlpha = 0.01f;
    public float FilterMaxAlpha = 0.2f;

    public bool EnableSubOsc = false;
    public float SubOscLevel = 0.5f; // 0 = silent, 1 = same as main
    public int SubOscOctaveOffset = -1; // usually -1 for one octave below

    public bool EnableDistortion = false;
    public float DistortionAmount = 0.8f; // 0 = clean, 1 = hard clip

    public bool EnablePitchDrift = false;
    public float DriftFrequencyHz = 0.5f; // how fast the pitch wobbles
    public float DriftAmountCents = 5f;   // how wide it wobbles (cents = 1/100 semitone)
    public float Velocity = 1f; // default full velocity
}

public static class SynthPatches
{
    public static SynthPatch Guitar => new SynthPatch
    {
        Waveform = WaveformType.PluckedString, // New waveform
        EnableTransient = false,               // Let the pluck algorithm handle the burst
        EnableLowPassFilter = true,            // Smooth the highs
        EnableDynamicFilter = false,           // Keep static filtering for simplicity
        FilterAlpha = 0.02f,

        EnableSubOsc = false,                  // Skip for realism
        EnableDistortion = false,              // Turn off harshness
        EnablePitchDrift = false,              // Plucked strings are stable

        TransientDurationSeconds = 0.01f,      // Ignored here but safe to leave
        Envelope = new ADSREnvelope
        {
            Attack = 0.005,
            Decay = 0.1,
            Sustain = 0.25,
            Release = 0.4
        }
    };

    public static SynthPatch Bass => new SynthPatch
    {
        Waveform = WaveformType.Sine,
        EnableTransient = false,

        EnableLowPassFilter = true,
        EnableDynamicFilter = false,
        FilterAlpha = 0.01f, // Let more mids through post-distortion

        EnableSubOsc = true,
        SubOscLevel = 0.6f,
        SubOscOctaveOffset = -1,

        EnableDistortion = true,
        DistortionAmount = 0.2f, // Just enough for growl

        EnablePitchDrift = false,

        Envelope = new ADSREnvelope
        {
            Attack = 0.005,
            Decay = 0.12,
            Sustain = 0.5,   // Increase sustain to avoid piano dropoff
            Release = 0.4
        }
    };

    public static SynthPatch Sine => new SynthPatch
    {
        Waveform = WaveformType.Sine,
        EnableTransient = false,
        EnableLowPassFilter = false,
        Envelope = new ADSREnvelope
        {
            Attack = 0.01,
            Decay = 0.1,
            Sustain = 0.9,
            Release = 0.3
        }
    };
}
