using NAudio.Wave;

namespace klooie;

internal class RecyclableSampleProvider : RecyclableAudioProvider
{
    private const int EdgeFadeMilliseconds = 4;
    private static readonly int EdgeFadeFrameCount = Math.Max(1, (SoundProvider.SampleRate * EdgeFadeMilliseconds) / 1000);

    // ==== backing source (one of these is set) ====
    private CachedSound? sound;              // cached SFX (float[])
    private ISampleProvider? stream;         // streaming music (decoder pipeline)

    private long position;                   // index into sound.AudioData (samples)
    private ILifetime? maxLifetime;
    private int maxLifetimeLease;
    private EventLoop eventLoop;
    private bool loop;                       // only used for CachedSound path
    private long framesRendered;
    private int releaseFramesRemaining;
    private float releaseLeft;
    private float releaseRight;

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
            BeginRelease();
        }

        var releaseOnly = WriteReleaseTail(buffer, offset, count);
        if (releaseOnly > 0 || releaseFramesRemaining == 0 && (sound == null && stream == null)) return releaseOnly;

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
                BeginRelease();
                return WriteReleaseTail(buffer, offset, count);
            }

            // Apply volume + pan per frame (interleaved stereo)
            int frames = read / channels;
            int i = 0;
            int idx = offset;
            float vol = effectiveVolume;
            while (i < frames)
            {
                var left = buffer[idx] * vol * leftGain;
                var right = buffer[idx + 1] * vol * rightGain;
                ApplyFadeIn(ref left, ref right);
                buffer[idx] = left;
                buffer[idx + 1] = right;
                releaseLeft = left;
                releaseRight = right;
                idx += 2;
                i++;
            }

            // Streaming can end on a non zero read. If that happens then we need to
            // detect it. Otherwise playback will continue indefinitely with silence.
            if (read < count)
            {
                BeginRelease();
                read += WriteReleaseTail(buffer, offset + read, count - read);
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
                BeginRelease();
                break;
            }

            int framesToCopy = Math.Min(availableSamples / channels, count / channels);

            int srcIdx = intPosition;
            int dstIdx = offset;
            int frames = framesToCopy;
            float vol = effectiveVolume;

            while (frames-- > 0)
            {
                float l = src.AudioData[srcIdx] * vol * leftGain;
                float r = src.AudioData[srcIdx + 1] * vol * rightGain;
                ApplyFadeIn(ref l, ref r);
                buffer[dstIdx] = l;
                buffer[dstIdx + 1] = r;
                releaseLeft = l;
                releaseRight = r;
                srcIdx += 2;
                dstIdx += 2;
            }

            int samplesCopied = framesToCopy * channels;
            position += samplesCopied;
            offset += samplesCopied;
            count -= samplesCopied;
            totalSamplesWritten += samplesCopied;
        }

        if (count > 0)
        {
            totalSamplesWritten += WriteReleaseTail(buffer, offset, count);
        }

        return totalSamplesWritten;
    }

    private void ApplyFadeIn(ref float left, ref float right)
    {
        if (framesRendered < EdgeFadeFrameCount)
        {
            var gain = (framesRendered + 1f) / EdgeFadeFrameCount;
            left *= gain;
            right *= gain;
        }

        framesRendered++;
    }

    private void BeginRelease()
    {
        if (releaseFramesRemaining > 0) return;

        if (MathF.Abs(releaseLeft) < 0.0001f && MathF.Abs(releaseRight) < 0.0001f)
        {
            releaseFramesRemaining = 0;
            sound = null;
            stream = null;
            ScheduleDisposal();
            return;
        }

        releaseFramesRemaining = EdgeFadeFrameCount;
    }

    private int WriteReleaseTail(float[] buffer, int offset, int count)
    {
        if (releaseFramesRemaining <= 0) return 0;

        var channels = WaveFormat.Channels;
        var framesToWrite = Math.Min(count / channels, releaseFramesRemaining);
        var idx = offset;
        for (var i = 0; i < framesToWrite; i++)
        {
            var gain = releaseFramesRemaining / (float)EdgeFadeFrameCount;
            buffer[idx] = releaseLeft * gain;
            buffer[idx + 1] = releaseRight * gain;
            idx += channels;
            releaseFramesRemaining--;
        }

        if (releaseFramesRemaining == 0)
        {
            ScheduleDisposal();
            sound = null;
            stream = null;
        }

        return framesToWrite * channels;
    }

    private void ScheduleDisposal() => 
        SoundProvider.Current.EventLoop.Invoke(this, static me => me.Dispose());
      
    protected override void OnReturn()
    {
        base.OnReturn();
        try { (stream as IDisposable)?.Dispose(); } catch { }
        sound = null;
        stream = null;
        eventLoop = null;
        maxLifetime = null;
        maxLifetimeLease = -1;
        loop = false;
        position = 0;
        framesRendered = 0;
        releaseFramesRemaining = 0;
        releaseLeft = 0;
        releaseRight = 0;
    }
}
