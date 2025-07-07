using NAudio.Wave;

namespace klooie;



public class SynthVoiceProvider : RecyclableAudioProvider, IReleasableNote
{
    private float frequency;
    private static WaveFormat waveFormat = new WaveFormat(CachedSound.ExpectedSampleRate, CachedSound.ExpectedBitsPerSample, CachedSound.ExpectedChannels);
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
    public bool isDone;
    public static readonly LazyPool<SynthVoiceProvider> _pool = new(() => new SynthVoiceProvider());

    private SynthVoiceProvider() { }

    public static SynthVoiceProvider Create(float frequencyHz, SynthPatch patch, VolumeKnob master, VolumeKnob? knob)
    {
        var ret = _pool.Value.Rent();
        ret.frequency = frequencyHz;
        ret.sampleRate = waveFormat.SampleRate;
        ret.time = 0;
        ret.filteredSample = 0;
        ret.InitVolume(master, knob);
        knob.Dispose();
        ret.patch = patch;
        ret.isDone = false;

        patch.Envelope.Trigger(0, ret.sampleRate);

        if (patch.EnablePitchDrift)
        {
            ret.driftPhase = 0;
            ret.driftPhaseIncrement = (float)(2 * Math.PI * patch.DriftFrequencyHz / ret.sampleRate);
            ret.driftRandomOffset = Random.Shared.NextSingle() * (2 * MathF.PI);
        }

        if (patch.Waveform == WaveformType.PluckedString)
        {
            int delaySamples = (int)(waveFormat.SampleRate / frequencyHz);
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
        if (isDone)
        {
            AudioPlaybackEngine.EventLoop.Invoke(this, Recyclable.TryDisposeMe);
            return 0;
        }

        int samplesWritten = 0;
        for (int n = 0; n < count; n += 2)
        {
            float level = patch.Envelope.GetLevel(time);

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

        return samplesWritten;
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

        double phase = 2 * Math.PI * driftedFrequency * t;
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
        patch.Dispose();
        patch = null!;
        pluckBuffer = null;
        pluckLength = 0;
        pluckWriteIndex = 0;
        base.OnReturn();
    }
}


