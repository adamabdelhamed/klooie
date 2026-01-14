using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace klooie;
internal sealed class SoundCache
{
    private readonly IBinarySoundProvider? provider;
    private readonly Dictionary<string, CachedSound> cached;



    public SoundCache(IBinarySoundProvider? provider)
    {
        this.provider = provider;
        cached = new Dictionary<string, CachedSound>(StringComparer.OrdinalIgnoreCase);
    }

    public RecyclableSampleProvider? GetSample(
     EventLoop eventLoop,
     string? soundId,
     VolumeKnob masterVolume,
     VolumeKnob? sampleVolume,
     ILifetime? maxLifetime,
     bool loop,
     bool isMusic)
    {
        if (provider == null) return null;
        if (string.IsNullOrEmpty(soundId)) return null;
        if (provider.Contains(soundId) == false) return null;

        if (isMusic)
        {
            // Stream: no big float[]; tiny, steady memory
            var streaming = new StreamingMusicProvider(provider, soundId, loop);
            return RecyclableSampleProvider.Create(eventLoop, streaming, masterVolume, sampleVolume, maxLifetime);
        }

        // SFX: keep cached in RAM (usually short)
        if (!cached.TryGetValue(soundId, out var cachedSound))
        {
            cachedSound = new CachedSound(provider, soundId);
            cached[soundId] = cachedSound;
        }

        return RecyclableSampleProvider.Create(eventLoop, cachedSound, masterVolume, sampleVolume, maxLifetime, loop);
    }

    public void Clear()
    {
        cached.Clear();
        GC.Collect();
    }
}


internal sealed class CachedSound
{
    public float[] AudioData { get; set; }
    public int SampleCount { get; set; }
    public WaveFormat WaveFormat { get; set; }

    public string SoundId { get; set; }
    public CachedSound(IBinarySoundProvider provider, string soundId)
    {
        LoadFrom(provider, soundId);
    }


    public CachedSound() { }


    /// <summary>
    /// Loads (or reloads) audio data from the provider into this instance.
    /// If the current capacity is insufficient, the buffer is grown according to <paramref name="growPolicy"/>.
    /// </summary>
    public void LoadFrom(IBinarySoundProvider provider, string soundId)
    {
        SoundId = soundId;

        using var mp3Stream = provider.Open(soundId);
        using var mp3Reader = new Mp3FileReader(mp3Stream);
        using var wavStream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(wavStream, mp3Reader);
        wavStream.Position = 0;
        using var wavReader = new WaveFileReader(wavStream);

        // Guard: Check format
        var wf = wavReader.WaveFormat;
        if (wf.SampleRate != SoundProvider.SampleRate || wf.Channels != SoundProvider.ChannelCount || wf.BitsPerSample != SoundProvider.BitsPerSample || wf.Encoding != WaveFormatEncoding.Pcm)
        {
            throw new InvalidOperationException( $"WAV format mismatch. Expected {SoundProvider.ChannelCount}ch, {SoundProvider.SampleRate}Hz, {SoundProvider.BitsPerSample}-bit PCM. " + $"Got {wf.Channels}ch, {wf.SampleRate}Hz, {wf.BitsPerSample}-bit {wf.Encoding}.");
        }

        WaveFormat = wf;

        long totalSamples = wavReader.Length / (SoundProvider.BitsPerSample / 8);
        int sampleCount = checked((int)totalSamples);
        var sampleProvider = wavReader.ToSampleProvider();
        if(AudioData == null || AudioData.Length < sampleCount) AudioData = new float[sampleCount];

        int totalRead = 0;
        while (totalRead < sampleCount)
        {
            int read = sampleProvider.Read(AudioData, totalRead, sampleCount - totalRead);
            if (read == 0) break;
            totalRead += read;
        }

        if (totalRead != sampleCount)
        {
            throw new InvalidOperationException(
                $"Expected to read {sampleCount} samples, but only read {totalRead}. " +
                "This may indicate an issue with the WAV file or its format.");
        }

        SampleCount = totalRead;
    }
}

static class PcmCache
{
    public static string GetOrBuild(string assetId, IBinarySoundProvider provider)
    {
        var cachePath = Path.Combine(Path.GetTempPath(), $"ttbs_{assetId}.wav");
        if (File.Exists(cachePath)) return cachePath;

        // decode once; pick your poison: WMF or Mp3FileReader
        using var s = provider.Open(assetId);
        using WaveStream decoder =
            s is FileStream fs ? new MediaFoundationReader(fs.Name)
                               : new Mp3FileReader(s); // fallback

        using var outFs = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        WaveFileWriter.WriteWavFileToStream(outFs, decoder); // streamed, not MemoryStream
        return cachePath;
    }
}

public readonly struct MusicVizFrame
{
    public readonly float Rms;      // 0..1-ish (normalized)
    public readonly float Peak;     // 0..1-ish
    public readonly float BeatHint; // 0..1-ish (optional, cheap)
    public MusicVizFrame(float rms, float peak, float beatHint) { Rms = rms; Peak = peak; BeatHint = beatHint; }
}

 
public sealed class StreamingMusicProvider : ISampleProvider, IDisposable
{
    public static Event<MusicVizFrame>? MusicVizEvent { get; private set; }

 
    // Call from app thread
    public static void InitializeMusicVizEvent(ILifetime lt)
    {
        if(Thread.CurrentThread.ManagedThreadId != SoundProvider.Current.EventLoop.ThreadId)  throw new InvalidOperationException("Must be called from the sound event loop thread.");
        if (MusicVizEvent != null) throw new NotSupportedException("Already initialized");
        MusicVizEvent = Event<MusicVizFrame>.Create();
        lt.OnDisposed(() =>
        {
            MusicVizEvent.Dispose();
            MusicVizEvent = null;
        });
    }

    private WaveStream pcm;
    private ISampleProvider pipe;
    private readonly bool loop;

    // viz state
    private float env;              // smoothed RMS
    private float peakEnv;          // slower peak
    private float beat;             // cheap beat-ish impulse
    private float beatHold;
    private int samplesSinceLastPublish;

    // tune these
    private const int PublishHz = 60;
    private const float Attack = 0.45f;     // larger = faster rise
    private const float Release = 0.12f;    // smaller = slower fall
    private const float PeakRelease = 0.01f;

    public StreamingMusicProvider(IBinarySoundProvider p, string id, bool loop)
    {
        var wavPath = PcmCache.GetOrBuild(id, p);
        pcm = new WaveFileReader(wavPath);
        ISampleProvider s = pcm.ToSampleProvider();

        if (pcm.WaveFormat.Channels != SoundProvider.ChannelCount)
            s = pcm.WaveFormat.Channels == 2 && SoundProvider.ChannelCount == 1
                ? new StereoToMonoSampleProvider(s) { LeftVolume = .5f, RightVolume = .5f }
                : new MonoToStereoSampleProvider(s);

        if (pcm.WaveFormat.SampleRate != SoundProvider.SampleRate)
            s = new WdlResamplingSampleProvider(s, SoundProvider.SampleRate);

        pipe = s;
        this.loop = loop;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int n = pipe.Read(buffer, offset + total, count - total);
            if (n > 0)
            {
                // --- compute stats over this chunk ---
                AccumulateViz(buffer, offset + total, n);

                total += n;
                continue;
            }

            if (loop) { pcm.Position = 0; continue; }
            return 0;
        }

        return total;
    }

    private void AccumulateViz(float[] buffer, int offset, int n)
    {
        // RMS + peak over the block
        double sumSq = 0;
        float peak = 0f;

        int end = offset + n;
        for (int i = offset; i < end; i++)
        {
            float x = buffer[i];
            float ax = x < 0 ? -x : x;
            if (ax > peak) peak = ax;
            sumSq += (double)x * x;
        }

        float rms = (float)Math.Sqrt(sumSq / n);

        // normalize-ish (you can calibrate later)
        rms = Math.Clamp(rms * 2.2f, 0f, 1f);
        peak = Math.Clamp(peak * 1.6f, 0f, 1f);

        // envelope follower
        env = Lerp(env, rms, rms > env ? Attack : Release);
        peakEnv = Lerp(peakEnv, peak, peak > peakEnv ? 0.25f : PeakRelease);

        // cheap beat hint:
        // if instantaneous rms jumps above a moving envelope by a margin, emit an impulse that decays.
        float diff = rms - env;
        if (diff > 0.18f && peak > 0.35f)
        {
            beatHold = 1f;   // impulse
        }
        else
        {
            beatHold *= 0.92f; // decay
        }
        beat = beatHold;

        // publish at ~60Hz
        samplesSinceLastPublish += n;
        int samplesPerPublish = SoundProvider.SampleRate / PublishHz;
        if (samplesSinceLastPublish >= samplesPerPublish)
        {
            samplesSinceLastPublish -= samplesPerPublish;

            // must fire on app thread
            if(MusicVizEvent != null) SoundProvider.Current.EventLoop.Invoke(new MusicVizFrame(env, peakEnv, beat), (viz) => MusicVizEvent?.Fire(viz));
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    public void Dispose() { pipe = null; pcm?.Dispose(); }
}


