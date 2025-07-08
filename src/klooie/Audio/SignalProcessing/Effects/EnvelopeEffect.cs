using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
class EnvelopeEffect : Recyclable, IEffect
{
    public ADSREnvelope Envelope { get; private set; }

    private static readonly LazyPool<EnvelopeEffect> _pool = new(() => new EnvelopeEffect());

    private EnvelopeEffect() { }

    public static EnvelopeEffect Create(double attack, double decay, double sustain, double release)
    {
        var fx = _pool.Value.Rent();
        fx.Envelope = ADSREnvelope.Create();
        fx.Envelope.Attack = attack;
        fx.Envelope.Decay = decay;
        fx.Envelope.Sustain = sustain;
        fx.Envelope.Release = release;
        fx.Envelope.Trigger(0, SoundProvider.SampleRate);
        return fx;
    }

    public IEffect Clone() => Create(
        Envelope.Attack,
        Envelope.Decay,
        Envelope.Sustain,
        Envelope.Release);

    public float Process(float input, int frameIndex, float time)
    {
        return input * Envelope.GetLevel(time);
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
