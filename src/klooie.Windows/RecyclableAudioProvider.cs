using NAudio.Wave;

namespace klooie;

public abstract class RecyclableAudioProvider : Recyclable, ISampleProvider
{
    protected VolumeKnob masterKnob;
    protected VolumeKnob? sampleKnob;
    protected bool trackMasterVolumeChanges;
    protected float fixedMasterVolume;
    protected float? fixedSampleVolume;
    protected float fixedSamplePan;
    protected float effectiveVolume;
    protected float effectivePan;

    public abstract WaveFormat WaveFormat { get; }
    public abstract int Read(float[] buffer, int offset, int count);

    public void InitVolume(VolumeKnob master, VolumeKnob? sample)
    {
        masterKnob = master ?? throw new ArgumentNullException(nameof(master));
        sampleKnob = sample;
        fixedSampleVolume = null;
        fixedSamplePan = 0f;
        trackMasterVolumeChanges = true;

        master.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sample?.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sample?.PanChanged.Subscribe(this, static (me, v) => me.OnPanChanged(), this);

        OnVolumeChanged();
        OnPanChanged();
    }

    public void InitFixedVolume(VolumeKnob master, float sampleVolume, float samplePan)
    {
        masterKnob = master ?? throw new ArgumentNullException(nameof(master));
        sampleKnob = null;
        fixedSampleVolume = Math.Clamp(sampleVolume, 0f, 1f);
        fixedSamplePan = Math.Clamp(samplePan, -1f, 1f);
        trackMasterVolumeChanges = false;
        fixedMasterVolume = master.Volume;
        OnVolumeChanged();
        OnPanChanged();
    }

    protected void OnPanChanged()
    {
        float pan = sampleKnob?.Pan ?? fixedSamplePan;
        effectivePan = Math.Max(-1f, Math.Min(1f, pan));
    }

    protected void OnVolumeChanged()
    {
        float master = (float)Math.Pow(trackMasterVolumeChanges ? masterKnob.Volume : fixedMasterVolume, 1.2f);
        float sample = (float)Math.Pow(fixedSampleVolume ?? sampleKnob?.Volume ?? 1, 1.2f);
        effectiveVolume = Math.Clamp(master * sample, 0f, 1f);
    }

    protected override void OnReturn()
    {
        sampleKnob?.TryDispose("external/klooie/src/klooie.Windows/RecyclableAudioProvider.cs:62");
        sampleKnob = null;
        masterKnob = null;
        trackMasterVolumeChanges = false;
        fixedMasterVolume = 0f;
        fixedSampleVolume = null;
        fixedSamplePan = 0f;
        effectiveVolume = 0f;
        effectivePan = 0f;
        base.OnReturn();
    }
}
