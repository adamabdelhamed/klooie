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
    private bool isDone;
    private ConsoleApp app;
    public static readonly LazyPool<SynthVoiceProvider> _pool = new(() => new SynthVoiceProvider());

    private SynthVoiceProvider() { }

    public static SynthVoiceProvider Create(float frequencyHz, SynthPatch patch, VolumeKnob master, VolumeKnob? knob)
    {
        var ret = _pool.Value.Rent();
        ret.app = ConsoleApp.Current ?? throw new InvalidOperationException("SynthVoiceProvider requires a ConsoleApp to be running. Please start a ConsoleApp before using SynthVoiceProvider.");
        ret.frequency = frequencyHz;
        ret.sampleRate = waveFormat.SampleRate;
        ret.time = 0;
        ret.filteredSample = 0;
        ret.InitVolume(master, knob);
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
            app.Invoke(this, Recyclable.TryDisposeMe);
            return 0;
        }
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
