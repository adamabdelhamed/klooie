using System.Runtime.CompilerServices;

namespace klooie;

[SynthDocumentation("""
Applies an Attack-Decay-Sustain-Release envelope to shape the level of the
incoming signal.
""")]
[SynthCategory("Dynamics")]
public class EnvelopeEffect : Recyclable, IEffect
{
    public ADSREnvelope Envelope { get; private set; }

    private static readonly LazyPool<EnvelopeEffect> _pool = new(() => new EnvelopeEffect());

    private EnvelopeEffect() { }

    [SynthDocumentation("""
Timing values used to construct the ADSR envelope.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Silence time before the attack begins (seconds).
""")]
        public double Delay;

        [SynthDocumentation("""
Duration of the attack phase in seconds.
""")]
        public double Attack;

        [SynthDocumentation("""
Time for the level to drop from the peak to the
sustain level.
""")]
        public double Decay;

        [SynthDocumentation("""
Normalized sustain level (0–1) held until release.
""")]
        public double Sustain;

        [SynthDocumentation("""
Time for the level to fade out after a note is
released.
""")]
        public double Release;
    }

    // Backward-compatible factory: no delay (0)
    public static EnvelopeEffect Create(double attack, double decay, double sustain, double release) =>
        Create(new Settings() { Delay = 0, Attack = attack, Decay = decay, Sustain = sustain, Release = release });

    // New overload with Delay
    public static EnvelopeEffect Create(double delay, double attack, double decay, double sustain, double release) =>
        Create(new Settings() { Delay = delay, Attack = attack, Decay = decay, Sustain = sustain, Release = release });

    public static EnvelopeEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.Envelope = ADSREnvelope.Create();
        fx.Envelope.Delay = settings.Delay;
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
            Delay = Envelope.Delay,
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

    public double Delay;    // seconds (NEW)
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
        Delay = 0;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double SafeDiv(double num, double den) => den <= 0 ? (num > 0 ? 1.0 : 0.0) : (num / den);

    public float GetLevel(double currentTime)
    {
        // Time since the very start
        double tSinceOn = currentTime - noteOnTime;

        // Still in the pre-attack delay? Output silence.
        if (tSinceOn < Delay) return 0f;

        // Shift time origin to the envelope start (right after delay)
        double t = tSinceOn - Delay;

        if (!isReleased)
        {
            if (t < Attack) return (float)SafeDiv(t, Attack);

            if (t < Attack + Decay)
            {
                double decayTime = t - Attack;
                return (float)(1.0 - (1.0 - Sustain) * SafeDiv(decayTime, Decay));
            }

            return (float)Sustain;
        }
        else
        {
            // Released at this absolute time:
            double tSinceOffAbs = currentTime - noteOffTime!.Value;

            // Where in the envelope were we at release? (also relative to delay)
            double tAtRelease = noteOffTime.Value - noteOnTime - Delay;

            float startLevel;
            if (tAtRelease <= 0)
            {
                // Release occurred before the delayed attack started: we were at 0.
                startLevel = 0f;
            }
            else if (tAtRelease < Attack)
            {
                startLevel = (float)SafeDiv(tAtRelease, Attack);
            }
            else if (tAtRelease < Attack + Decay)
            {
                double decayTime = tAtRelease - Attack;
                startLevel = (float)(1.0 - (1.0 - Sustain) * SafeDiv(decayTime, Decay));
            }
            else
            {
                startLevel = (float)Sustain;
            }

            float releaseLevel = (float)(startLevel * Math.Max(0.0, 1.0 - SafeDiv(tSinceOffAbs, Release)));
            return Math.Max(0f, releaseLevel);
        }
    }

    public bool IsDone(double currentTime)
    {
        if (!isReleased) return false;
        return currentTime - noteOffTime!.Value >= Release;
    }
}