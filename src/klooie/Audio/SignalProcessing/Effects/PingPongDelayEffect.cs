using klooie;

[SynthDescription("""
Delay effect that bounces echoes between the left and right speakers,
creating a ping‑pong stereo field.
""")]
[SynthCategory("Delay")]
public class PingPongDelayEffect : Recyclable, IEffect
{
    private float[] leftBuffer, rightBuffer;
    private int bufferSize, writeIndex;
    private float feedback, mix;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;
    private int delaySamples;

    private float prevLeftOut, prevRightOut;

    private static readonly LazyPool<PingPongDelayEffect> _pool = new(() => new PingPongDelayEffect());

    private PingPongDelayEffect() { }

    [SynthDescription("""
    Settings that define the delay time, feedback and how the echoes mix with
    the original signal.
    """)]
    public struct Settings
    {
        [SynthDescription("""Length of the delay line in samples.""")]
        public int DelaySamples;

        [SynthDescription("""Portion of the delayed signal fed back for more
        repeats (0 = none, 1 = infinite).""")]
        public float Feedback;

        [SynthDescription("""Mix between original and delayed signals (0 = dry,
        1 = fully wet).""")]
        public float Mix;

        [SynthDescription("""If true, harder notes produce a stronger delay
        mix.""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""Function mapping velocity to a mix multiplier.""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static PingPongDelayEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.delaySamples = settings.DelaySamples;
        fx.feedback = Math.Clamp(settings.Feedback, 0f, 0.98f);
        fx.mix = Math.Clamp(settings.Mix, 0f, 1f);
        fx.velocityAffectsMix = settings.VelocityAffectsMix;
        fx.mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;

        fx.bufferSize = Math.Max(2, settings.DelaySamples + 1);
        fx.leftBuffer = fx.leftBuffer != null && fx.leftBuffer.Length == fx.bufferSize ? fx.leftBuffer : new float[fx.bufferSize];
        fx.rightBuffer = fx.rightBuffer != null && fx.rightBuffer.Length == fx.bufferSize ? fx.rightBuffer : new float[fx.bufferSize];
        fx.writeIndex = 0;
        fx.prevLeftOut = fx.prevRightOut = 0f;
        Array.Clear(fx.leftBuffer, 0, fx.bufferSize);
        Array.Clear(fx.rightBuffer, 0, fx.bufferSize);

        return fx;
    }

    // Only processes mono (L+R alternation; true stereo can be more advanced)
    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        // Simulate "frameIndex % 2" as L/R for stereo buffer writing
        bool isLeft = (ctx.FrameIndex & 1) == 0;

        // Calculate read index (circular buffer)
        int readIndex = (writeIndex + 1) % bufferSize;

        float delayed = isLeft ? rightBuffer[readIndex] : leftBuffer[readIndex];
        float dry = input;
        float wet = delayed;

        // Write to buffer: current input plus feedback from other side
        if (isLeft)
            leftBuffer[writeIndex] = input + feedback * prevRightOut;
        else
            rightBuffer[writeIndex] = input + feedback * prevLeftOut;

        // Store output for feedback
        if (isLeft)
            prevLeftOut = wet;
        else
            prevRightOut = wet;

        // Advance buffer only once per stereo frame
        if (!isLeft)
            writeIndex = (writeIndex + 1) % bufferSize;

        float mixAmt = mix;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return dry * (1f - mixAmt) + wet * mixAmt;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            DelaySamples = delaySamples,
            Feedback = feedback,
            Mix = mix,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve
        };
        return Create(in settings);
    }

    protected override void OnReturn()
    {
        leftBuffer = rightBuffer = null;
        bufferSize = writeIndex = 0;
        prevLeftOut = prevRightOut = 0f;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}
