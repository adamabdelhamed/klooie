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
sealed class StreamingMusicProvider : ISampleProvider, IDisposable
{
    private WaveStream pcm;            // WaveFileReader on the cached WAV
    private ISampleProvider pipe;      // + resample/mono/stereo as needed
    private readonly bool loop;

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
                total += n;
                continue;
            }

            if (loop)
            {
                pcm.Position = 0; // loop with no reallocation
                continue;
            }

            // ✅ EOF: explicitly return 0 to tell RecyclableSampleProvider to dispose
            return 0;
        }
        return total;
    }


    public void Dispose() { pipe = null; pcm?.Dispose(); }
}

