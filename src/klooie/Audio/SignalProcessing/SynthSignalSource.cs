using System;
using System.Collections.Generic;
using System.Threading;

namespace klooie;

public class SynthSignalSource : Recyclable
{
    private static readonly LazyPool<SynthSignalSource> _pool = new(() => new SynthSignalSource());

    // DSP & signal state
    private float frequency;
    private double sampleRate;
    private float time;
    private float filteredSample;
    private SynthPatch patch;

    private float driftPhase;
    private float driftPhaseIncrement;
    private float driftRandomOffset;
    private float[]? pluckBuffer;
    private int pluckLength;
    private int pluckWriteIndex;
    public bool isDone;
    protected VolumeKnob masterKnob;
    protected VolumeKnob? sampleKnob;
    protected float effectiveVolume;
    protected float effectivePan;

    private List<SignalProcess> pipeline;

    public ADSREnvelope Envelope
    {
        get
        {
            var envelopeEffect = patch.Effects[patch.Effects.Count - 1] as EnvelopeEffect;
            if(envelopeEffect == null) throw new InvalidOperationException("Last effect must be EnvelopeEffect");
            return envelopeEffect.Envelope;
        }
    }
    public bool IsDone => isDone;

    private static int _globalId = 1;
    public int Id { get; private set; }

    public virtual void ReleaseNote() => Envelope.ReleaseNote(time);

    protected SynthSignalSource() { }

    public static SynthSignalSource Create(float frequencyHz, SynthPatch patch, VolumeKnob master, VolumeKnob? knob)
    {
        var ret = _pool.Value.Rent();
        ret.Id = Interlocked.Increment(ref _globalId);
        ret.Construct(frequencyHz, patch, master, knob);
        return ret;
    }

    private static Comparison<IEffect> EnvelopesAtTheEndSortComparison = (a, b) =>
    {
        if (a is EnvelopeEffect && b is EnvelopeEffect)
            return 0;
        if (a is EnvelopeEffect)
            return 1;
        if (b is EnvelopeEffect)
            return -1;
        return 0;
    };

    protected void Construct(float frequencyHz, SynthPatch patch, VolumeKnob master, VolumeKnob? knob)
    {
        frequency = frequencyHz;
        sampleRate = 44100;
        time = 0;
        filteredSample = 0;
        this.patch = patch;
        this.patch.Effects.Items.Sort(EnvelopesAtTheEndSortComparison);
        isDone = false;
        driftPhase = 0f;
        driftPhaseIncrement = (float)(2 * Math.PI * patch.DriftFrequencyHz / sampleRate);
        driftRandomOffset = Random.Shared.NextSingle() * (2 * MathF.PI);

        InitVolume(master, knob);
        knob?.Dispose();

        Envelope.Trigger(0, sampleRate);

        // Pluck buffer if needed
        pluckBuffer = null;
        pluckLength = 0;
        pluckWriteIndex = 0;
        if (patch.Waveform == WaveformType.PluckedString)
        {
            int delaySamples = (int)(sampleRate / frequencyHz);
            pluckBuffer = new float[delaySamples];
            pluckLength = delaySamples;
            pluckWriteIndex = 0;
            for (int i = 0; i < delaySamples; i++)
                pluckBuffer[i] = (float)(Random.Shared.NextDouble() * 2.0 - 1.0);
        }

        // Build DSP pipeline
        pipeline = pipeline ?? new List<SignalProcess>(20);
        pipeline.Add(OscillatorStage);
        if (patch.EnableSubOsc)
            pipeline.Add(SubOscillatorStage);
        if (patch.EnableTransient)
            pipeline.Add(TransientStage);


        // After all core DSP, add effect stages
        if (patch.Effects != null)
        {
            foreach (var effect in patch.Effects.Items)
            {
                pipeline.Add(effect.Process);
            }
        }
    }

    // ---- DSP Pipeline delegate signature ----
    public delegate float SignalProcess(float input, int frameIndex, float time);

    // ---- Pipeline stages: all instance methods, no capturing lambdas ----

    private float OscillatorStage(float input, int frameIndex, float time)
    {
        return Oscillate(time);
    }

    private float SubOscillatorStage(float input, int frameIndex, float time)
    {
        float subTime = time * (float)Math.Pow(2, patch.SubOscOctaveOffset);
        return input + Oscillate(subTime, WaveformType.Sine) * patch.SubOscLevel;
    }

    private float TransientStage(float input, int frameIndex, float time)
    {
        return time < patch.TransientDurationSeconds
            ? input + (float)(Random.Shared.NextDouble() * 2 - 1) * 0.3f
            : input;
    }
 

    // ---- Feature logic ----

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
        float damping = 0.98f;
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
        pipeline?.Clear();   
        base.OnReturn();
    }

    public virtual int Render(float[] buffer, int offset, int count)
    {
        if (isDone)
        {
            SoundProvider.Current.EventLoop.Invoke(this, Recyclable.TryDisposeMe);
            return 0;
        }

        int samplesWritten = 0;
        float localTime = time;
        for (int n = 0; n < count; n += 2)
        {
            time = localTime;

            float sample = 0f;
            for (int s = 0; s < pipeline.Count; s++)
                sample = pipeline[s](sample, n / 2, time);

            float finalSample = Math.Clamp(sample * effectiveVolume, -1f, 1f);

            buffer[offset + n] = finalSample;
            buffer[offset + n + 1] = finalSample;

            localTime += 1f / (float)sampleRate;
            samplesWritten += 2;

            if (Envelope.IsDone(time))
            {
                isDone = true;
                break;
            }
        }

        // Zero the rest of the buffer if not fully filled
        for (int i = offset + samplesWritten; i < offset + count; i++)
            buffer[i] = 0f;

        return samplesWritten;
    }
}
