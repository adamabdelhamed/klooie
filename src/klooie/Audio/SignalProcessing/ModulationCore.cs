using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace klooie;

public enum ModCurve : byte { Linear, Exp, Log }

public interface IModSpec
{
    IModSource Bind(in ModBindContext ctx);
    bool IsBipolar { get; }
}

public readonly struct ModBindContext
{
    public readonly float SampleRate;
    public readonly IEnvelope AmpEnvelope;
    public readonly NoteExpression Note;
    public ModBindContext(float sampleRate, IEnvelope ampEnv, NoteExpression note) { SampleRate = sampleRate; AmpEnvelope = ampEnv; Note = note; }
}

public struct RouteSpec
{
    public IModSpec Spec;
    public float Depth;
    public ModCurve Curve;
}

public struct ModParamSpec
{
    public float Base;
    public float Min, Max;
    // Smooth is a lag factor in [0,1). 0 = no smoothing (instant), 0.9 = heavy smoothing (slow response)
    public float Smooth;
    private RouteSpec r0, r1, r2, r3;
    private byte count;

    public static implicit operator ModParamSpec(float baseVal) => new ModParamSpec { Base = baseVal, Min = float.NegativeInfinity, Max = float.PositiveInfinity, Smooth = 0f, count = 0 };

    public ModParamSpec WithClamp(float min, float max) { Min = min; Max = max; return this; }
    public ModParamSpec WithSmoothing(float s) { Smooth = s; return this; }

    public ModParamSpec Route(IModSpec spec, float depth, ModCurve curve = ModCurve.Linear)
    {
        var rs = new RouteSpec { Spec = spec, Depth = depth, Curve = curve };
        switch (count) { case 0: r0 = rs; break; case 1: r1 = rs; break; case 2: r2 = rs; break; case 3: r3 = rs; break; default: return this; }
        if (count < 4) count++;
        return this;
    }

    internal byte Count => count;
    internal RouteSpec GetRoute(byte i) => i switch { 0 => r0, 1 => r1, 2 => r2, 3 => r3, _ => default };
 
}

public static class ModParamSpecDsl
{
    public static ModParamSpec Param(this float baseVal) => (ModParamSpec)baseVal;
}

public interface IModSource
{
    float Next(in VoiceCtx v);
    bool IsBipolar { get; }
}

public readonly struct VoiceCtx
{
    public readonly double Time, TNote;
    public readonly float Velocity01, SampleRate, BPM;
    public readonly int MidiNote;
    public readonly bool Gate;
    public VoiceCtx(double time, double tNote, float vel, int midi, float sr, float bpm, bool gate) { Time = time; TNote = tNote; Velocity01 = vel; MidiNote = midi; SampleRate = sr; BPM = bpm; Gate = gate; }
}

public struct BoundRoute
{
    public IModSource Source;
    public float Depth;
    public ModCurve Curve;
}

public struct ModParam
{
    public float Base, Min, Max, Smooth, Current;
    private BoundRoute r0, r1, r2, r3;
    private byte count;

    public static ModParam Bind(in ModParamSpec spec, in ModBindContext bindCtx)
    {
        var p = new ModParam { Base = spec.Base, Min = spec.Min, Max = spec.Max, Smooth = spec.Smooth, Current = spec.Base, count = 0 };
        for (byte i = 0; i < spec.Count; i++)
        {
            var rs = spec.GetRoute(i);
            var src = rs.Spec.Bind(bindCtx);
            switch (p.count)
            {
                case 0: p.r0 = new BoundRoute { Source = src, Depth = rs.Depth, Curve = rs.Curve }; break;
                case 1: p.r1 = new BoundRoute { Source = src, Depth = rs.Depth, Curve = rs.Curve }; break;
                case 2: p.r2 = new BoundRoute { Source = src, Depth = rs.Depth, Curve = rs.Curve }; break;
                case 3: p.r3 = new BoundRoute { Source = src, Depth = rs.Depth, Curve = rs.Curve }; break;
            }
            if (p.count < 4) p.count++;
        }
        return p;
    }

    // Curve transforms a 0..1 (unipolar) or -1..1 (bipolar mapped to [-1,1]) input.
    // NOTE: Exp and Log currently assume non-negative input; bipolar sources will skew positive.
    // Future enhancement: add bipolar-aware variants to preserve symmetry.
    private static float Curve(float x, ModCurve c) => c switch
    {
        ModCurve.Exp => (x <= 0f) ? 0f : (MathF.Pow(2f, x) - 1f),
        ModCurve.Log => MathF.Log2(MathF.Max(1f + x, 1e-6f)),
        _ => x
    };

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public float Update(in VoiceCtx v)
    {
        float val = Base;
        switch (count)
        {
            case 4: val += Curve(r3.Source.Next(v), r3.Curve) * r3.Depth; goto case 3;
            case 3: val += Curve(r2.Source.Next(v), r2.Curve) * r2.Depth; goto case 2;
            case 2: val += Curve(r1.Source.Next(v), r1.Curve) * r1.Depth; goto case 1;
            case 1: val += Curve(r0.Source.Next(v), r0.Curve) * r0.Depth; break;
        }
        if (val < Min) val = Min; else if (val > Max) val = Max;
        if (Smooth > 0f) Current += (val - Current) * (1f - Smooth);
        else Current = val;
        return Current;
    }
}

public enum LfoShape : byte { Sine, Triangle, Square, SawUp, RandomSH }

public struct LfoSpec : IModSpec
{
    public LfoShape Shape;
    public float RateHz;
    public float Phase01;
    public bool Bipolar; public bool IsBipolar => Bipolar;
    public IModSource Bind(in ModBindContext ctx) => new LfoSource(Shape, RateHz, Phase01, Bipolar, ctx.SampleRate);
    private sealed class LfoSource : IModSource
    {
        private readonly LfoShape shape; private readonly bool bipolar; private float phase01; private readonly float step; private float lastRand;
        public LfoSource(LfoShape s, float rateHz, float phase, bool bi, float sr) { shape = s; bipolar = bi; phase01 = phase; step = rateHz / sr; }
        public bool IsBipolar => bipolar;
        public float Next(in VoiceCtx v)
        {
            float x = shape switch
            {
                LfoShape.Sine => FastSin01(phase01),
                LfoShape.Triangle => 1f - 4f * MathF.Abs(phase01 - 0.5f),
                LfoShape.Square => phase01 < 0.5f ? 1f : -1f,
                LfoShape.SawUp => (phase01 + phase01) - 1f,
                LfoShape.RandomSH => (phase01 + step >= 1f ? (lastRand = Random.Shared.NextSingle() * 2f - 1f) : lastRand),
                _ => 0f
            };
            phase01 += step; if (phase01 >= 1f) phase01 -= 1f;
            return bipolar ? x : 0.5f * (x + 1f);
        }
        private static float FastSin01(float t) { float x = (t - MathF.Floor(t)) * 2f - 1f; float x2 = x * x; return x * (1.27323954f - 0.405284735f * x2); }
    }
}

public readonly struct AmpEnvSpec : IModSpec
{
    public bool IsBipolar => false;
    public IModSource Bind(in ModBindContext ctx) => new EnvSrc(ctx.AmpEnvelope);
    private sealed class EnvSrc : IModSource { private readonly IEnvelope e; public EnvSrc(IEnvelope env) { e = env; } public bool IsBipolar => false; public float Next(in VoiceCtx v) => e.GetLevel((float)v.Time); }
}

public readonly struct VelocitySpec : IModSpec
{
    public bool IsBipolar => false;
    private sealed class VelSrc : IModSource { public static readonly VelSrc Instance = new(); public bool IsBipolar => false; public float Next(in VoiceCtx v) => v.Velocity01; }
    public IModSource Bind(in ModBindContext ctx) => VelSrc.Instance;
}

public static class Lfo { public static LfoSpec Sine(float rateHz, float phase01 = 0f, bool bipolar = true) => new LfoSpec { Shape = LfoShape.Sine, RateHz = rateHz, Phase01 = phase01, Bipolar = bipolar }; public static LfoSpec Tri(float rateHz, float phase01 = 0f, bool bipolar = true) => new LfoSpec { Shape = LfoShape.Triangle, RateHz = rateHz, Phase01 = phase01, Bipolar = bipolar }; }

public static class Env { public static AmpEnvSpec Amp() => new AmpEnvSpec(); }
public static class Vel { public static VelocitySpec In01() => new VelocitySpec(); }

public interface IVoiceBindableEffect { void BindForVoice(in ModBindContext ctx); }
public interface IControlRateUpdatable { void UpdateControl(in VoiceCtx v); }