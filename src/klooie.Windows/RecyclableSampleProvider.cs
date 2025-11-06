using NAudio.Wave;

namespace klooie;

internal class RecyclableSampleProvider : RecyclableAudioProvider
{
    // ==== backing source (one of these is set) ====
    private CachedSound? sound;              // cached SFX (float[])
    private ISampleProvider? stream;         // streaming music (decoder pipeline)

    private long position;                   // index into sound.AudioData (samples)
    private ILifetime? maxLifetime;
    private int maxLifetimeLease;
    private EventLoop eventLoop;
    private bool loop;                       // only used for CachedSound path

    private static LazyPool<RecyclableSampleProvider> pool = new(() => new RecyclableSampleProvider());

    public static RecyclableSampleProvider Create(
        EventLoop eventLoop,
        CachedSound sound,
        VolumeKnob masterKnob,
        VolumeKnob? sampleKnob,
        ILifetime? loopLifetime,
        bool loop)
    {
        var ret = pool.Value.Rent();
        ret.Construct(eventLoop, sound, masterKnob, sampleKnob, loopLifetime, loop);
        return ret;
    }

    // NEW: wrapper for streaming sources (music). Looping is handled by the upstream ISampleProvider.
    public static RecyclableSampleProvider Create(
        EventLoop loopCtx,
        ISampleProvider sample,
        VolumeKnob master,
        VolumeKnob? perSample,
        ILifetime? maxLifetime)
    {
        var ret = pool.Value.Rent();
        ret.Construct(loopCtx, sample, master, perSample, maxLifetime);
        return ret;
    }

    // ---- CachedSound path ----
    protected void Construct(EventLoop eventLoop, CachedSound src, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? loopLifetime, bool loop)
    {
        this.eventLoop = eventLoop;
        this.sound = src;
        this.stream = null;
        this.loop = loop;
        this.maxLifetime = loopLifetime;
        this.maxLifetimeLease = loopLifetime?.Lease ?? -1;
        this.position = 0;
        InitVolume(masterKnob, sampleKnob);
    }

    // ---- Streaming path ----
    protected void Construct(EventLoop eventLoop, ISampleProvider src, VolumeKnob masterKnob, VolumeKnob? sampleKnob, ILifetime? maxLifetime)
    {
        this.eventLoop = eventLoop;
        this.sound = null;
        this.stream = src;
        this.loop = false; // upstream ISampleProvider should implement looping if desired
        this.maxLifetime = maxLifetime;
        this.maxLifetimeLease = maxLifetime?.Lease ?? -1;
        this.position = 0;
        InitVolume(masterKnob, sampleKnob);
    }

    public override WaveFormat WaveFormat =>
        stream != null ? stream.WaveFormat :
        sound != null ? sound.WaveFormat :
        WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount); // fallback

    public override int Read(float[] buffer, int offset, int count)
    {
        if (maxLifetime != null && !maxLifetime.IsStillValid(maxLifetimeLease))
        {
            ScheduleDisposal();
            return 0;
        }

        // Equal-power pan coefficients (stereo expected)
        int channels = WaveFormat.Channels;
        if (channels != 2) throw new NotSupportedException("Only stereo samples supported");

        double angle = (Math.PI / 4) * (effectivePan + 1); // -1..+1 → 0..π/2
        float leftGain = (float)Math.Cos(angle);
        float rightGain = (float)Math.Sin(angle);

        // Streaming path: pull directly from upstream ISampleProvider, then apply volume/pan in-place.
        if (stream != null)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0)
            {
                ScheduleDisposal();
                return 0;
            }

            // Apply volume + pan per frame (interleaved stereo)
            int frames = read / channels;
            int i = 0;
            int idx = offset;
            float vol = effectiveVolume;
            while (i < frames)
            {
                buffer[idx] *= vol * leftGain;
                buffer[idx + 1] *= vol * rightGain;
                idx += 2;
                i++;
            }
            return read;
        }

        // CachedSound path: copy from the big float[] and apply volume/pan while copying.
        var src = sound!;
        int totalSamplesWritten = 0;

        while (count > 0)
        {
            int intPosition = (int)position;
            int availableSamples = src.SampleCount - intPosition;
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

            int srcIdx = intPosition;
            int dstIdx = offset;
            int frames = framesToCopy;
            float vol = effectiveVolume;

            while (frames-- > 0)
            {
                float l = src.AudioData[srcIdx];
                float r = src.AudioData[srcIdx + 1];
                buffer[dstIdx] = l * vol * leftGain;
                buffer[dstIdx + 1] = r * vol * rightGain;
                srcIdx += 2;
                dstIdx += 2;
            }

            int samplesCopied = framesToCopy * channels;
            position += samplesCopied;
            offset += samplesCopied;
            count -= samplesCopied;
            totalSamplesWritten += samplesCopied;
        }

        return totalSamplesWritten;
    }

    private void ScheduleDisposal() =>
        SoundProvider.Current.EventLoop.Invoke(this, static me => me.Dispose());

    protected override void OnReturn()
    {
        base.OnReturn();
        sound = null;
        stream = null;
        eventLoop = null;
        maxLifetime = null;
        maxLifetimeLease = -1;
        loop = false;
        position = 0;
    }
}
