using NAudio.Wave;

namespace klooie;
internal class RecyclableSampleProvider : Recyclable, ISampleProvider
{
    private long position;
    private CachedSound sound;
    private ILifetime? maxLifetime;
    private int maxLifetimeLease;
    private bool loop;
    private EventLoop eventLoop;
    public WaveFormat WaveFormat => sound.WaveFormat;
    private VolumeKnob masterKnob;
    private VolumeKnob? sampleKnob;
    private float effectiveVolume;
    private float effectivePan;

    private static LazyPool<RecyclableSampleProvider> pool = new LazyPool<RecyclableSampleProvider>(() => new RecyclableSampleProvider());

    public static RecyclableSampleProvider Create(EventLoop eventLoop, CachedSound sound, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        var ret = pool.Value.Rent();
        ret.Construct(eventLoop, sound, masterKnob, sampleKnob, loopLifetime, loop);
        return ret;
    }

    protected virtual void Construct(EventLoop eventLoop, CachedSound sound, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        this.eventLoop = eventLoop ?? throw new ArgumentNullException(nameof(eventLoop));
        this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
        this.masterKnob = masterKnob ?? throw new ArgumentNullException(nameof(masterKnob));
        this.sampleKnob = sampleKnob;

        masterKnob.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sampleKnob?.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sampleKnob?.PanChanged.Subscribe(this, static (me, v) => me.OnPanChanged(), this);
        OnVolumeChanged();
        OnPanChanged();
        this.position = 0;
        this.maxLifetime = loopLifetime;
        this.maxLifetimeLease = loopLifetime?.Lease ?? -1;
        this.loop = loop;
    }

    private void OnPanChanged()
    {
        float pan = sampleKnob?.Pan ?? 0f; // Default to center pan if no sample knob
        effectivePan = Math.Max(-1f, Math.Min(1f, pan)); // Clamp pan to -1 (left) to 1 (right)
        if (effectivePan < -1f || effectivePan > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(pan), "Pan must be between -1 and 1.");
        }
    }

    private void OnVolumeChanged()
    {
        float master = (float)Math.Pow(masterKnob.Volume, 1.5f);
        float sample = (float)Math.Pow(sampleKnob?.Volume ?? 1, 1.5f);
        effectiveVolume = master * sample;
        if (effectiveVolume < 0.0001f)
        {
            effectiveVolume = 0f; // Avoid underflow issues
        }
        if (effectiveVolume > 1f)
        {
            effectiveVolume = 1f; // Clamp to maximum volume
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (maxLifetime != null && !maxLifetime.IsStillValid(maxLifetimeLease))
        {
            ScheduleDisposal();
            return 0;
        }

        int totalSamplesWritten = 0;

        // For stereo: each sample is 2 floats (L, R)
        int channels = sound.WaveFormat.Channels;
        if (channels != 2) throw new NotSupportedException("Panning only supported for stereo samples");

        // Precompute gains
        float pan = effectivePan; // -1=left, 0=center, 1=right
        double angle = (Math.PI / 4) * (pan + 1); // map -1..1 to 0..PI/2
        float leftGain = (float)Math.Cos(angle);
        float rightGain = (float)Math.Sin(angle);
        float vol = effectiveVolume;

        while (count > 0)
        {
            int intPosition = (int)position;
            int availableSamples = sound.SampleCount - intPosition;
            if (availableSamples <= 0)
            {
                if (loop)
                {
                    position = 0;
                    continue;
                }
                else
                {
                    ScheduleDisposal();
                    break;
                }
            }

            int framesAvailable = availableSamples / channels;
            int framesToCopy = Math.Min(framesAvailable, count / channels);

            for (int i = 0; i < framesToCopy; i++)
            {
                int srcIdx = intPosition + i * channels;
                float srcLeft = sound.AudioData[srcIdx];
                float srcRight = sound.AudioData[srcIdx + 1];

                // Mix: apply gain and pan
                buffer[offset + i * 2] = srcLeft * vol * leftGain;
                buffer[offset + i * 2 + 1] = srcRight * vol * rightGain;
            }

            int samplesToCopy = framesToCopy * channels;
            position += samplesToCopy;
            offset += samplesToCopy;
            count -= samplesToCopy;
            totalSamplesWritten += samplesToCopy;
        }

        return totalSamplesWritten;
    }


    private void ScheduleDisposal() => eventLoop.Invoke(this, static (o) =>
    {
        TryDisposeMe(o);
        return Task.CompletedTask;
    });
    

    protected override void OnReturn()
    {
        base.OnReturn();
        sound = null;
        position = 0;
        maxLifetime = default;
        maxLifetimeLease = -1;
        loop = false;
        eventLoop = null;
        sampleKnob?.TryDispose();
        sampleKnob = null;
        masterKnob = null;
        effectiveVolume = 0f;
        effectivePan = 0f;
    }
}
