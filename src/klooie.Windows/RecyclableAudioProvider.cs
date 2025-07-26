using NAudio.Wave;

namespace klooie;

public abstract class RecyclableAudioProvider : Recyclable, ISampleProvider
{
    protected VolumeKnob masterKnob;
    protected VolumeKnob? sampleKnob;
    protected float effectiveVolume;
    protected float effectivePan;

    public abstract WaveFormat WaveFormat { get; }
    public abstract int Read(float[] buffer, int offset, int count);

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
        SoundProvider.DisposeIfNotNull(sampleKnob);
        sampleKnob = null;
        masterKnob = null;
        effectiveVolume = 0f;
        effectivePan = 0f;
        base.OnReturn();
    }
}
