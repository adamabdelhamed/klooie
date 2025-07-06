using NAudio.Wave;

namespace klooie;
internal class RecyclableSampleProvider : RecyclableAudioProvider
{
    private long position;
    private CachedSound sound;
    private ILifetime? maxLifetime;
    private int maxLifetimeLease;
    private bool loop;
    private EventLoop eventLoop;

    private static LazyPool<RecyclableSampleProvider> pool = new(() => new RecyclableSampleProvider());

    public static RecyclableSampleProvider Create(EventLoop eventLoop, CachedSound sound, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        var ret = pool.Value.Rent();
        ret.Construct(eventLoop, sound, masterKnob, sampleKnob, loopLifetime, loop);
        return ret;
    }

    protected void Construct(EventLoop eventLoop, CachedSound sound, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        this.eventLoop = eventLoop;
        this.sound = sound;
        this.loop = loop;
        this.maxLifetime = loopLifetime;
        this.maxLifetimeLease = loopLifetime?.Lease ?? -1;
        this.position = 0;
        InitVolume(masterKnob, sampleKnob);
    }

    public override WaveFormat WaveFormat => sound.WaveFormat;

    public override int Read(float[] buffer, int offset, int count)
    {
        if (maxLifetime != null && !maxLifetime.IsStillValid(maxLifetimeLease))
        {
            ScheduleDisposal();
            return 0;
        }

        int totalSamplesWritten = 0;
        int channels = sound.WaveFormat.Channels;
        if (channels != 2) throw new NotSupportedException("Only stereo samples supported");

        double angle = (Math.PI / 4) * (effectivePan + 1);
        float leftGain = (float)Math.Cos(angle);
        float rightGain = (float)Math.Sin(angle);

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
                ScheduleDisposal();
                break;
            }

            int framesToCopy = Math.Min(availableSamples / channels, count / channels);
            for (int i = 0; i < framesToCopy; i++)
            {
                int srcIdx = intPosition + i * channels;
                float srcLeft = sound.AudioData[srcIdx];
                float srcRight = sound.AudioData[srcIdx + 1];
                buffer[offset + i * 2] = srcLeft * effectiveVolume * leftGain;
                buffer[offset + i * 2 + 1] = srcRight * effectiveVolume * rightGain;
            }

            int samplesCopied = framesToCopy * channels;
            position += samplesCopied;
            offset += samplesCopied;
            count -= samplesCopied;
            totalSamplesWritten += samplesCopied;
        }

        return totalSamplesWritten;
    }

    private void ScheduleDisposal()
    {
        eventLoop.Invoke(this, static o => { TryDisposeMe(o); return Task.CompletedTask; });
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        sound = null;
        eventLoop = null;
        maxLifetime = null;
        maxLifetimeLease = -1;
        loop = false;
        position = 0;
    }
}

