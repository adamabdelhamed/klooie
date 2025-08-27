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
        public Func<double, double?> AttackCurve;   // returns progress 0..1 (or null when done)
        public Func<double, double?> DecayCurve;    // returns progress 0..1 (or null when done)
        public Func<double, double> SustainCurve;  // absolute level 0..1 over sustain time
        public Func<double, double?> ReleaseCurve;  // returns progress 0..1 (or null when done)
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

    // phase functions (providers think in normalized progress 0..1; null = “done”)
    public Func<double, double?> AttackCurve = Linear01;
    public Func<double, double?> DecayCurve = Linear01;
    public Func<double, double> SustainCurve = One;      // absolute level
    public Func<double, double?> ReleaseCurve = Linear01;

    private double noteOnTime;
    private double? noteOffTime;

    private enum Phase { Delay, Attack, Decay, Sustain, Release, Done }
    private Phase phase;
    private double phaseStartTime;

    // Internal level mapping (we scale curves to actual levels here)
    private double lastLevel;            // last computed absolute level (0..1)

    private double decayStartLevel;      // level at the start of Decay
    private double decayTargetLevel;     // snapshot of SustainCurve(0) taken at Decay start

    private double releaseStartLevel;    // level at the start of Release

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

        lastLevel = 0;
        decayStartLevel = 0;
        decayTargetLevel = 0;
        releaseStartLevel = 0;
    }

    public void Trigger(double currentTime, double sampleRate)
    {
        noteOnTime = currentTime;
        noteOffTime = null;
        phase = Delay > 0 ? Phase.Delay : Phase.Attack;
        phaseStartTime = currentTime;

        lastLevel = 0;
        decayStartLevel = 1; // default; will be overwritten on Attack→Decay transition
        // decayTargetLevel is captured when we actually enter Decay
    }

    public void ReleaseNote(double currentTime)
    {
        if (phase == Phase.Done || phase == Phase.Release) return;

        // Snapshot current level WITHOUT advancing phase, so release starts exactly at the audible level now.
        var levelNow = SnapshotLevel(currentTime);

        noteOffTime = currentTime;
        releaseStartLevel = levelNow;
        phase = Phase.Release;
        phaseStartTime = currentTime;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

    // Provider helpers
    private static double? Linear01(double t) => t; // progress (not clamped); we clamp & end-detect internally
    private static double One(double _) => 1.0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Map(double start, double target, double progress)
        => start + (target - start) * progress;

    // Normalizes provider output to progress 0..1 and flags “ended” on null or >=1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double progress, bool ended) NormalizeProgress(double? p)
    {
        if (p == null) return (1.0, true);
        var prog = Clamp01(p.Value);
        return (prog, prog >= 1.0);
    }

    public float GetLevel(double currentTime)
    {
        if (phase == Phase.Done) return 0f;

        double tPhase = currentTime - phaseStartTime;

        switch (phase)
        {
            case Phase.Delay:
                {
                    if (tPhase >= Delay)
                    {
                        phase = Phase.Attack;
                        phaseStartTime = currentTime;
                        return GetLevel(currentTime);
                    }
                    lastLevel = 0.0;
                    return 0f;
                }

            case Phase.Attack:
                {
                    var (prog, ended) = NormalizeProgress(AttackCurve?.Invoke(tPhase));
                    var level = Map(0.0, 1.0, prog);
                    lastLevel = level;

                    if (ended)
                    {
                        // Prepare Decay start & target using final Attack level and Sustain snapshot
                        decayStartLevel = level;                   // where attack actually ended
                        decayTargetLevel = Clamp01(SustainCurve(0)); // sustain level at sustain start
                        phase = Phase.Decay;
                        phaseStartTime = currentTime;
                    }
                    return (float)level;
                }

            case Phase.Decay:
                {
                    var (prog, ended) = NormalizeProgress(DecayCurve?.Invoke(tPhase));
                    var level = Map(decayStartLevel, decayTargetLevel, prog);
                    lastLevel = level;

                    if (ended)
                    {
                        phase = Phase.Sustain;
                        phaseStartTime = currentTime;
                    }
                    return (float)level;
                }

            case Phase.Sustain:
                {
                    var level = Clamp01(SustainCurve?.Invoke(tPhase) ?? 0.0);
                    lastLevel = level;
                    return (float)level;
                }

            case Phase.Release:
                {
                    var (prog, ended) = NormalizeProgress(ReleaseCurve?.Invoke(tPhase));
                    var level = Map(releaseStartLevel, 0.0, prog);
                    lastLevel = level;

                    if (ended)
                    {
                        phase = Phase.Done;
                    }
                    return (float)level;
                }

            default: // Done
                lastLevel = 0.0;
                return 0f;
        }
    }

    public bool IsDone(double currentTime) => phase == Phase.Done;

    // Computes the instantaneous level WITHOUT advancing phases (used to seed Release correctly).
    private double SnapshotLevel(double currentTime)
    {
        var tPhase = currentTime - phaseStartTime;

        return phase switch
        {
            Phase.Delay => 0.0,
            Phase.Attack => Map(0.0, 1.0, Clamp01(AttackCurve?.Invoke(tPhase) ?? 1.0)),
            Phase.Decay => Map(decayStartLevel, decayTargetLevel, Clamp01(DecayCurve?.Invoke(tPhase) ?? 1.0)),
            Phase.Sustain => Clamp01(SustainCurve?.Invoke(tPhase) ?? 0.0),
            Phase.Release => Map(releaseStartLevel, 0.0, Clamp01(ReleaseCurve?.Invoke(tPhase) ?? 1.0)),
            _ => 0.0
        };
    }
}

public static class ADSRCurves
{
    public static double? Instant(double t) => null;
    public static double NoSustain(double t) => 0;


    public static double? Linear(double t, double duration)
    {
        if (t > duration) return null;
        var progress = t / duration; // 0→1
        return progress;
    }

    public static double? Quadratic(double t, double duration)
    {
        if (t > duration) return null;
        var progress = t / duration;
        return progress * progress;
    }

    public static double? Cubic(double t, double duration)
    {
        if (t > duration) return null;
        var progress = t / duration;
        return progress * progress * progress;
    }

    public static double? Power(double t, double duration, int power)
    {
        if (t > duration) return null;
        var progress = t / duration;
        return Math.Pow(progress, power);
    }
}