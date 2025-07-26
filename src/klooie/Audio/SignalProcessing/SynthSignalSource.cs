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
    private List<IPitchModEffect>? pitchMods;
    private List<SignalProcess> pipeline;
    private ADSREnvelope envelope;
    private float? noteReleaseTime = null; // in seconds

    // --- Phase accumulator for main oscillator ---
    private double oscPhase = 0.0;
    private double oscPhaseSub = 0.0; // For sub-oscillator

    public bool IsDone => isDone;

    private static int _globalId = 1;
    public int Id { get; private set; }

    // === LFO support ===
    public enum LfoTarget { Pitch, FilterCutoff, Amp, Pan }

    public struct LfoSettings
    {
        public LfoTarget Target;
        public float RateHz;
        public float Depth;
        public int Shape; // 0=sine, 1=triangle
        public float PhaseOffset;
        public bool VelocityAffectsDepth;
        public Func<float, float>? DepthVelocityCurve;
    }

    private struct LfoState
    {
        public LfoSettings Settings;
        public float Phase;
        public float PhaseInc;
    }

    private LfoState[]? lfos;

    public virtual void ReleaseNote()
    {
        noteReleaseTime = time;
        envelope.ReleaseNote(time);
    }

    protected SynthSignalSource() { }

    public static SynthSignalSource Create(float frequencyHz, SynthPatch patch, VolumeKnob master, NoteExpression note)
    {
        var ret = _pool.Value.Rent();
        ret.Id = Interlocked.Increment(ref _globalId);
        ret.Construct(frequencyHz, patch, master, note, null);
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

    private NoteExpression note;
    private EffectContext ctx;

    protected void Construct(float frequencyHz, SynthPatch patch, VolumeKnob master, NoteExpression note, VolumeKnob? knob)
    {
        frequency = patch.FrequencyOverride.HasValue ? patch.FrequencyOverride.Value : frequencyHz;
        sampleRate = 44100;
        time = 0;
        filteredSample = 0;
        this.patch = patch;
        this.note = note;
        ctx = new EffectContext { Note = note };
        this.patch.Effects.Items.Sort(EnvelopesAtTheEndSortComparison);
        isDone = false;
        driftPhase = 0f;
        driftPhaseIncrement = (float)(2 * Math.PI * patch.DriftFrequencyHz / sampleRate);
        driftRandomOffset = Random.Shared.NextSingle() * (2 * MathF.PI);

        oscPhase = 0.0;
        oscPhaseSub = 0.0;

        // === LFO: initialize state per patch ===
        if (patch.Lfos != null && patch.Lfos.Count > 0)
        {
            lfos = new LfoState[patch.Lfos.Count];
            for (int i = 0; i < lfos.Length; i++)
            {
                var set = patch.Lfos[i];
                lfos[i].Settings = set;
                lfos[i].Phase = set.PhaseOffset;
                lfos[i].PhaseInc = set.RateHz / (float)sampleRate;
            }
        }
        else
        {
            lfos = null;
        }

        InitVolume(master, knob);
        knob?.Dispose();
        this.envelope = patch.FindEnvelopeEffect().Envelope;
        envelope.Trigger(0, sampleRate);

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
        pipeline.Clear();
        pipeline.Add(OscillatorStage);
        if (patch.EnableSubOsc)
            pipeline.Add(SubOscillatorStage);
        if (patch.EnableTransient)
            pipeline.Add(TransientStage);

        pitchMods = patch.Effects.Items.OfType<IPitchModEffect>().ToList();
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
    public delegate float SignalProcess(in EffectContext ctx);

    // ---- Pipeline stages: all instance methods, no capturing lambdas ----

    // Main oscillator: uses the main phase accumulator
    private float Oscillate()
    {
        return Oscillate(ref oscPhase, frequency, patch.Waveform);
    }

    // Sub oscillator: pass in the sub-phase accumulator, frequency, and always sine wave
    private float OscillateSub()
    {
        float subFreq = frequency * (float)Math.Pow(2, patch.SubOscOctaveOffset);
        return Oscillate(ref oscPhaseSub, subFreq, WaveformType.Sine);
    }

    // Unified oscillator with phase accumulator (for both main and sub)
    private float Oscillate(ref double phase, float freq, WaveformType wave)
    {
        float totalCents = 0f;

        // Pitch Drift (only for main oscillator)
        if (wave == patch.Waveform && patch.EnablePitchDrift)
            totalCents += patch.DriftAmountCents * MathF.Sin(driftPhase + driftRandomOffset);

        // Vibrato LFO (only for main oscillator)
        if (wave == patch.Waveform && patch.EnableVibrato)
        {
            float vibratoPhase = 2 * MathF.PI * patch.VibratoRateHz * time + patch.VibratoPhaseOffset;
            totalCents += MathF.Sin(vibratoPhase) * patch.VibratoDepthCents;
        }

        if (pitchMods != null && wave == patch.Waveform)
        {
            var pmCtx = new PitchModContext { Time = time, ReleaseTime = noteReleaseTime, Note = note };
            for (int i = 0; i < pitchMods.Count; i++)
            {
                totalCents += pitchMods[i].GetPitchOffsetCents(pmCtx);
            }
        }

        // === LFO: apply LFO pitch modulation (cents) ===
        if (lfos != null)
        {
            for (int i = 0; i < lfos.Length; i++)
            {
                var st = lfos[i];
                if (st.Settings.Target == LfoTarget.Pitch)
                {
                    float lfo = 0f;
                    if (st.Settings.Shape == 0)
                        lfo = MathF.Sin(2 * MathF.PI * st.Phase);
                    else
                        lfo = 2f * MathF.Abs(2f * (st.Phase - MathF.Floor(st.Phase + 0.5f))) - 1f;

                    float lfoDepth = st.Settings.Depth;
                    if (st.Settings.VelocityAffectsDepth)
                        lfoDepth *= (st.Settings.DepthVelocityCurve ?? EffectContext.EaseLinear)(note.Velocity / 127f);

                    totalCents += lfo * lfoDepth;
                }
            }
        }

        float modulatedFrequency = freq * MathF.Pow(2f, totalCents / 1200f);

        driftPhase += driftPhaseIncrement;

        // Phase accumulator step
        double phaseIncr = 2.0 * Math.PI * modulatedFrequency / sampleRate;
        phase += phaseIncr;
        if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;
        if (phase < 0) phase += 2.0 * Math.PI;

        switch (wave)
        {
            case WaveformType.Sine:
                return (float)Math.Sin(phase);
            case WaveformType.Square:
                return MathF.Sign(MathF.Sin((float)phase));
            case WaveformType.Triangle:
                float frac = (float)(phase / (2.0 * Math.PI));
                return 2f * MathF.Abs(2f * (frac % 1f) - 1f) - 1f;
            case WaveformType.Saw:
                float sawFrac = (float)(phase / (2.0 * Math.PI));
                return 2f * (sawFrac - MathF.Floor(sawFrac + 0.5f));
            case WaveformType.Noise:
                return (float)(Random.Shared.NextDouble() * 2 - 1);
            case WaveformType.PluckedString:
                return GetPluckedSample();
            default:
                return 0f;
        }
    }

    // Pipeline stage for main oscillator
    private float OscillatorStage(in EffectContext ctx)
    {
        return Oscillate();
    }

    // Pipeline stage for sub oscillator (if enabled)
    private float SubOscillatorStage(in EffectContext ctx)
    {
        return ctx.Input + OscillateSub() * patch.SubOscLevel;
    }

    private float TransientStage(in EffectContext ctx)
    {
        return ctx.Time < patch.TransientDurationSeconds
            ? ctx.Input + (float)(Random.Shared.NextDouble() * 2 - 1) * 0.3f
            : ctx.Input;
    }

    private float pluckDamping = 0.98f; // Expose this as needed!

    private float GetPluckedSample()
    {
        if (pluckBuffer == null || pluckLength == 0)
            return 0;
        int nextIndex = (pluckWriteIndex + 1) % pluckLength;
        float current = pluckBuffer[pluckWriteIndex];
        float next = pluckBuffer[nextIndex];

        // Clamp for safety
        float damping = Math.Clamp(pluckDamping + (Random.Shared.NextSingle() - 0.5f) * 0.0002f, 0.8f, 0.999f);

        float newSample = damping * 0.5f * (current + next);

        // Avoid denormals
        if (Math.Abs(newSample) < 1e-10f) newSample = 0f;

        pluckBuffer[pluckWriteIndex] = newSample;
        pluckWriteIndex = nextIndex;

        // Optional: Also denormal protect current
        if (Math.Abs(current) < 1e-10f) current = 0f;

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
        envelope = null;
        noteReleaseTime = null;
        pipeline?.Clear();
        note = null!;
        ctx = default;
        oscPhase = 0.0;
        oscPhaseSub = 0.0;
        lfos = null;
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

            ctx.FrameIndex = n / 2;
            ctx.Time = time;
            ctx.Input = 0f;

            // === LFO: Compute non-pitch modulation ===
            float lfoCutoff = 0f, lfoAmp = 0f, lfoPan = 0f;
            if (lfos != null)
            {
                for (int i = 0; i < lfos.Length; i++)
                {
                    var st = lfos[i];
                    float lfo = 0f;
                    if (st.Settings.Shape == 0)
                        lfo = MathF.Sin(2 * MathF.PI * st.Phase);
                    else
                        lfo = 2f * MathF.Abs(2f * (st.Phase - MathF.Floor(st.Phase + 0.5f))) - 1f;

                    float lfoDepth = st.Settings.Depth;
                    if (st.Settings.VelocityAffectsDepth)
                        lfoDepth *= (st.Settings.DepthVelocityCurve ?? EffectContext.EaseLinear)(note.Velocity / 127f);

                    switch (st.Settings.Target)
                    {
                        case LfoTarget.FilterCutoff:
                            lfoCutoff += lfo * lfoDepth;
                            break;
                        case LfoTarget.Amp:
                            lfoAmp += lfo * lfoDepth;
                            break;
                        case LfoTarget.Pan:
                            lfoPan += lfo * lfoDepth;
                            break;
                    }

                    // Advance phase for all targets
                    lfos[i].Phase += lfos[i].PhaseInc;
                    if (lfos[i].Phase >= 1f) lfos[i].Phase -= 1f;
                }
            }

            float sample = 0f;
            for (int s = 0; s < pipeline.Count; s++)
            {
                ctx.Input = sample;
                sample = pipeline[s](ctx);
            }

            // === LFO: Apply non-pitch LFO modulations before output ===
            float finalSample = sample;

            // Apply LFO amplitude (tremolo)
            finalSample *= (1f + lfoAmp);

            // Apply pan (lfoPan)
            float pan = effectivePan + lfoPan;
            pan = Math.Max(-1f, Math.Min(1f, pan));

            float left = finalSample * (1 - pan) * effectiveVolume;
            float right = finalSample * (1 + pan) * effectiveVolume;

            left = Math.Clamp(left, -1f, 1f);
            right = Math.Clamp(right, -1f, 1f);

            buffer[offset + n] = left;
            buffer[offset + n + 1] = right;

            localTime += 1f / (float)sampleRate;
            samplesWritten += 2;

            if (envelope.IsDone(time))
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
