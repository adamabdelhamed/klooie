using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class SynthSignalSource : Recyclable
{
    private static readonly LazyPool<SynthSignalSource> _pool = new(() => new SynthSignalSource());

    private float frequency;
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
    protected VolumeKnob masterKnob;
    protected VolumeKnob? sampleKnob;
    protected float effectiveVolume;
    protected float effectivePan;

    public ADSREnvelope Envelope => patch.Envelope;
    public bool IsDone => isDone;
    public void ReleaseNote() => Envelope.ReleaseNote(time);
    private SynthSignalSource() { }

    public static SynthSignalSource Create(float frequencyHz, SynthPatch patch, VolumeKnob master, VolumeKnob? knob)
    {
        var ret = _pool.Value.Rent();
        ret.frequency = frequencyHz;
        ret.sampleRate = 44100;
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
            int delaySamples = (int)(44100 / frequencyHz);
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


    private float ApplyDistortion(float sample, float amount)
    {
        // amount: 0 = clean, 1 = full drive
        float drive = 1f + (10f - 1f) * amount;
        return MathF.Tanh(sample * drive);
    }

    private float Oscillate(float t, WaveformType? overrideWave = null)
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

    public void InitVolume(VolumeKnob master, VolumeKnob? sample)
    {
        masterKnob = master ?? throw new ArgumentNullException(nameof(master));
        sampleKnob = sample;

        master.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sample?.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sample?.PanChanged.Subscribe(this, static (me, v) => me.OnPanChanged(), this);

        OnVolumeChanged();
        OnPanChanged();
    }

    protected void OnPanChanged()
    {
        float pan = sampleKnob?.Pan ?? 0f;
        effectivePan = Math.Max(-1f, Math.Min(1f, pan));
    }

    protected void OnVolumeChanged()
    {
        float master = (float)Math.Pow(masterKnob.Volume, 1.2f);
        float sample = (float)Math.Pow(sampleKnob?.Volume ?? 1, 1.2f);
        effectiveVolume = Math.Clamp(master * sample, 0f, 1f);
    }

    protected override void OnReturn()
    {
        sampleKnob?.TryDispose();
        sampleKnob = null;
        masterKnob = null;
        effectiveVolume = 0f;
        effectivePan = 0f;
        base.OnReturn();
    }

    public int Render(float[] buffer, int offset, int count)
    {
        if (isDone)
        {
            SoundProvider.Current.EventLoop.Invoke(this, Recyclable.TryDisposeMe);
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

            float sample = Oscillate(time);

            if (patch.EnableSubOsc)
            {
                float subTime = time * (float)Math.Pow(2, patch.SubOscOctaveOffset);
                sample += Oscillate(subTime, overrideWave: WaveformType.Sine) * patch.SubOscLevel;
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
}