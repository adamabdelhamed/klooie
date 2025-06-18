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

    private static LazyPool<RecyclableSampleProvider> pool = new LazyPool<RecyclableSampleProvider>(() => new RecyclableSampleProvider());

    protected virtual void Construct(EventLoop eventLoop, CachedSound sound, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        this.eventLoop = eventLoop ?? throw new ArgumentNullException(nameof(eventLoop));
        this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
        this.masterKnob = masterKnob ?? throw new ArgumentNullException(nameof(masterKnob));
        this.sampleKnob = sampleKnob;

        masterKnob.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        sampleKnob?.VolumeChanged.Subscribe(this, static (me, v) => me.OnVolumeChanged(), this);
        OnVolumeChanged();
        this.position = 0;
        this.maxLifetime = loopLifetime;
        this.maxLifetimeLease = loopLifetime?.Lease ?? -1;
        this.loop = loop;
    }

    private void OnVolumeChanged()
    {
        float master = (float)Math.Pow(masterKnob.Volume, 2.0f);
        float sample = (float)Math.Pow(sampleKnob?.Volume ?? 1, 2.0f);
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

    public static RecyclableSampleProvider Create(EventLoop eventLoop, CachedSound sound, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        var ret = pool.Value.Rent();
        ret.Construct(eventLoop, sound, masterKnob, sampleKnob, loopLifetime, loop);
        return ret;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (maxLifetime != null && !maxLifetime.IsStillValid(maxLifetimeLease))
        {
            ScheduleDisposal();
            return 0;
        }

        int totalSamplesWritten = 0;

        while (count > 0)
        {
            int intPosition = (int)position;
            int availableSamples = sound.SampleCount - intPosition;
            if (availableSamples <= 0)
            {
                if (loop)
                {
                    position = 0;
                    continue; // loop back to start and try again
                }
                else
                {
                    ScheduleDisposal();
                    break;
                }
            }

            int samplesToCopy = Math.Min(availableSamples, count);

            var volumeForThisRead = effectiveVolume;
            if (volumeForThisRead == 1f)
            {
                Array.Copy(sound.AudioData, intPosition, buffer, offset, samplesToCopy);
            }
            else
            {
                for (int i = 0; i < samplesToCopy; i++)
                {
                    buffer[offset + i] = sound.AudioData[intPosition + i] * volumeForThisRead;
                }
            }

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
    }
}
