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

[SynthDocumentation("""
Applies an Attack-Decay-Sustain-Release envelope to shape the level of the
incoming signal.
""")]
[SynthCategory("Dynamics")]
public class CurvedEnvelopeEffect : Recyclable, IEffect
{
    public CurvedADSR Envelope { get; private set; }

    private static readonly LazyPool<CurvedEnvelopeEffect> _pool = new(() => new CurvedEnvelopeEffect());

    private CurvedEnvelopeEffect() { }

    [SynthDocumentation("""
Timing values used to construct the envelope.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Silence time before the attack begins (seconds).
""")]
        public double Delay;


        public Func<double,double?> Attack;


        public Func<double, double?> Decay;


        public Func<double, double> Sustain;

        public Func<double, double?> Release;
    }

    // Backward-compatible factory: no delay (0)
    public static CurvedEnvelopeEffect Create(Func<double, double?> attack, Func<double, double?> decay, Func<double, double> sustain, Func<double, double?> release) =>
        Create(new Settings() { Delay = 0, Attack = attack, Decay = decay, Sustain = sustain, Release = release });

    // New overload with Delay
    public static CurvedEnvelopeEffect Create(double delay, Func<double, double?> attack, Func<double, double?> decay, Func<double, double> sustain, Func<double, double?> release) =>
        Create(new Settings() { Delay = delay, Attack = attack, Decay = decay, Sustain = sustain, Release = release });

    public static CurvedEnvelopeEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.Envelope = CurvedADSR.Create(new CurvedADSR.Settings() { Delay = settings.Delay, AttackCurve = settings.Attack, DecayCurve = settings.Decay, SustainCurve = settings.Sustain, ReleaseCurve = settings.Release });
        fx.Envelope.Trigger(0, SoundProvider.SampleRate);
        return fx;
    }

    public IEffect Clone()
    {
        return Create(Envelope.Delay, Envelope.AttackCurve, Envelope.DecayCurve, Envelope.SustainCurve, Envelope.ReleaseCurve);
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

public interface IEnvelope
{
    double Delay { get; }
    void Trigger(double currentTime, double sampleRate);
    void ReleaseNote(double currentTime);
    float GetLevel(double currentTime);
    bool IsDone(double currentTime);
}

public class ADSREnvelope : Recyclable, IEnvelope
{
    private ADSREnvelope() { }
    private static LazyPool<ADSREnvelope> _pool = new(() => new ADSREnvelope());

    public static ADSREnvelope Create() => _pool.Value.Rent();

    public double Delay { get; set; }    // seconds (NEW)
    public double Attack { get; set; }    // seconds
    public double Decay { get; set; }     // seconds
    public double Sustain { get; set; }   // 0.0–1.0
    public double Release { get; set; }   // seconds

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

public sealed class CurvedADSR : Recyclable, IEnvelope
{
    public struct Settings
    {
        public double Delay; // seconds of silence before attack
        public Func<double, double?> AttackCurve;   // input: seconds since attack start
        public Func<double, double?> DecayCurve;    // input: seconds since decay start
        public Func<double, double> SustainCurve;   // input: seconds since sustain start
        public Func<double, double?> ReleaseCurve;  // input: seconds since release start
    }

    private CurvedADSR() { }
    private static readonly LazyPool<CurvedADSR> _pool = new(() => new CurvedADSR());
    public static CurvedADSR Create() => _pool.Value.Rent();

    public static CurvedADSR Create(in Settings s)
    {
        var env = Create();
        env.Delay = s.Delay;
        env.AttackCurve = s.AttackCurve ?? Linear01;
        env.DecayCurve = s.DecayCurve ?? Linear01;
        env.SustainCurve = s.SustainCurve ?? One;
        env.ReleaseCurve = s.ReleaseCurve ?? Linear01;
        return env;
    }

    public double Delay { get; private set; }

    // phase functions
    public Func<double, double?> AttackCurve = Linear01;
    public Func<double, double?> DecayCurve = Linear01;
    public Func<double, double> SustainCurve = One;
    public Func<double, double?> ReleaseCurve = Linear01;

    private double noteOnTime;
    private double? noteOffTime;

    private enum Phase { Delay, Attack, Decay, Sustain, Release, Done }
    private Phase phase;
    private double phaseStartTime;

    public IEnvelope Clone()
    {
        var c = Create();
        c.Delay = Delay;
        c.AttackCurve = AttackCurve;
        c.DecayCurve = DecayCurve;
        c.SustainCurve = SustainCurve;
        c.ReleaseCurve = ReleaseCurve;
        return c;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Delay = 0;
        AttackCurve = Linear01;
        DecayCurve = Linear01;
        SustainCurve = One;
        ReleaseCurve = Linear01;
        noteOnTime = 0;
        noteOffTime = null;
        phase = Phase.Done;
        phaseStartTime = 0;
    }

    public void Trigger(double currentTime, double sampleRate)
    {
        noteOnTime = currentTime;
        noteOffTime = null;
        phase = Delay > 0 ? Phase.Delay : Phase.Attack;
        phaseStartTime = currentTime;
    }

    public void ReleaseNote(double currentTime)
    {
        if (phase != Phase.Release && phase != Phase.Done)
        {
            noteOffTime = currentTime;
            phase = Phase.Release;
            phaseStartTime = currentTime;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    private static double? Linear01(double t) => Clamp01(t); // default: linear ramp
    private static double One(double _) => 1.0;

    public float GetLevel(double currentTime)
    {
        if (phase == Phase.Done) return 0f;

        double tPhase = currentTime - phaseStartTime;

        switch (phase)
        {
            case Phase.Delay:
                if (tPhase >= Delay)
                {
                    phase = Phase.Attack;
                    phaseStartTime = currentTime;
                    return GetLevel(currentTime);
                }
                return 0f;

            case Phase.Attack:
                var a = AttackCurve?.Invoke(tPhase);
                if (a == null) { phase = Phase.Decay; phaseStartTime = currentTime; return GetLevel(currentTime); }
                return (float)Clamp01(a.Value);

            case Phase.Decay:
                var d = DecayCurve?.Invoke(tPhase);
                if (d == null) { phase = Phase.Sustain; phaseStartTime = currentTime; return GetLevel(currentTime); }
                return (float)Clamp01(d.Value);

            case Phase.Sustain:
                return (float)Clamp01(SustainCurve?.Invoke(tPhase) ?? 0);

            case Phase.Release:
                var r = ReleaseCurve?.Invoke(tPhase);
                if (r == null) { phase = Phase.Done; return 0f; }
                return (float)Clamp01(r.Value);

            default: // Done
                return 0f;
        }
    }

    public bool IsDone(double currentTime) => phase == Phase.Done;
}
