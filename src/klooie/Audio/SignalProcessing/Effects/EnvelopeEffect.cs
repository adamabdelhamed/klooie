using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Applies an Attack‑Decay‑Sustain‑Release envelope to shape the level of the
incoming signal.
""")]
[SynthCategory("Dynamics")]
public class EnvelopeEffect : Recyclable, IEffect
{
    public ADSREnvelope Envelope { get; private set; }

    private static readonly LazyPool<EnvelopeEffect> _pool = new(() => new EnvelopeEffect());

    private EnvelopeEffect() { }

    [SynthDescription("""
Timing values used to construct the ADSR envelope.
""")]
    public struct Settings
    {
        [SynthDescription("""
Duration of the attack phase in seconds.
""")]
        public double Attack;

        [SynthDescription("""
Time for the level to drop from the peak to the
sustain level.
""")]
        public double Decay;

        [SynthDescription("""
Normalized sustain level (0–1) held until release.
""")]
        public double Sustain;

        [SynthDescription("""
Time for the level to fade out after a note is
released.
""")]
        public double Release;
    }

    public static EnvelopeEffect Create(double attack, double decay, double sustain, double release) =>
        Create(new Settings() { Attack = attack, Decay = decay, Sustain = sustain, Release = release });

    public static EnvelopeEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.Envelope = ADSREnvelope.Create();
        fx.Envelope.Attack = settings.Attack;
        fx.Envelope.Decay = settings.Decay;
        fx.Envelope.Sustain = settings.Sustain;
        fx.Envelope.Release = settings.Release;
        fx.Envelope.Trigger(0, SoundProvider.SampleRate);
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Attack = Envelope.Attack,
            Decay = Envelope.Decay,
            Sustain = Envelope.Sustain,
            Release = Envelope.Release
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        return ctx.Input * Envelope.GetLevel(ctx.Time);
    }

    public void Release(float time) => Envelope.ReleaseNote(time);

    public bool IsDone(float time) => Envelope.IsDone(time);

    protected override void OnReturn()
    {
        Envelope?.Dispose();
        Envelope = null!;
        base.OnReturn();
    }
}

public class ADSREnvelope : Recyclable
{
    private ADSREnvelope() { }
    private static LazyPool<ADSREnvelope> _pool = new(() => new ADSREnvelope());

    public static ADSREnvelope Create() => _pool.Value.Rent();

    public double Attack;   // seconds
    public double Decay;    // seconds
    public double Sustain;  // 0.0–1.0
    public double Release;  // seconds

    private double noteOnTime;
    private double? noteOffTime;
    private double sampleRate;
    private bool isReleased;

    protected override void OnReturn()
    {
        base.OnReturn();
        Attack = 0;
        Decay = 0;
        Sustain = 0;
        Release = 0;
        noteOnTime = 0;
        noteOffTime = null;
        sampleRate = 0;
        isReleased = false;
    }

    public void Trigger(double currentTime, double sampleRate)
    {
        this.noteOnTime = currentTime;
        this.sampleRate = sampleRate;
        this.noteOffTime = null;
        this.isReleased = false;
    }

    public void ReleaseNote(double currentTime)
    {
        if (!noteOffTime.HasValue)
        {
            noteOffTime = currentTime;
            isReleased = true;
        }
    }

    public float GetLevel(double currentTime)
    {
        double tSinceNoteOn = currentTime - noteOnTime;

        if (!isReleased)
        {
            if (tSinceNoteOn < Attack) return (float)(tSinceNoteOn / Attack);
            if (tSinceNoteOn < Attack + Decay)
            {
                double decayTime = tSinceNoteOn - Attack;
                return (float)(1.0 - (1.0 - Sustain) * (decayTime / Decay));
            }
            return (float)Sustain;
        }
        else
        {
            double tSinceNoteOff = currentTime - noteOffTime.Value;
            double tAtRelease = noteOffTime.Value - noteOnTime;

            float startLevel;

            if (tAtRelease < Attack)
            {
                startLevel = (float)(tAtRelease / Attack);
            }
            else if (tAtRelease < Attack + Decay)
            {
                double decayTime = tAtRelease - Attack;
                startLevel = (float)(1.0 - (1.0 - Sustain) * (decayTime / Decay));
            }
            else
            {
                startLevel = (float)Sustain;
            }

            float releaseLevel = (float)(startLevel * (1.0 - (tSinceNoteOff / Release)));
            return Math.Max(0f, releaseLevel);
        }
    }


    public bool IsDone(double currentTime)
    {
        if (!isReleased) return false;
        return currentTime - noteOffTime.Value >= Release;
    }
}