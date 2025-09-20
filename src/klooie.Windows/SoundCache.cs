using NAudio.Wave;

namespace klooie;
internal sealed class SoundCache
{
    private readonly IBinarySoundProvider? provider;
    private readonly Dictionary<string, CachedSound> cached;

    // Single, reusable buffer for music (only one song plays at a time).
    private CachedSound musicReusable = new CachedSound();

    public SoundCache(IBinarySoundProvider? provider)
    {
        this.provider = provider;
        cached = new Dictionary<string, CachedSound>(StringComparer.OrdinalIgnoreCase);
    }

    public RecyclableSampleProvider? GetSample(EventLoop eventLoop, string? soundId, VolumeKnob masterVolume, VolumeKnob? sampleVolume, ILifetime? maxLifetime, bool loop, bool isMusic)
    {
        if (provider == null) return null;
        if (string.IsNullOrEmpty(soundId)) return null;
        if (provider.Contains(soundId) == false) return null;

        if (isMusic)
        {
            musicReusable.LoadFrom(provider, soundId);
            return RecyclableSampleProvider.Create(eventLoop, musicReusable, masterVolume, sampleVolume, maxLifetime, loop);
        }
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
        musicReusable = null;
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

        using var stream = provider.Open(soundId);
        using var reader = new WaveFileReader(stream);

        // Guard: Check format
        var wf = reader.WaveFormat;
        if (wf.SampleRate != SoundProvider.SampleRate || wf.Channels != SoundProvider.ChannelCount || wf.BitsPerSample != SoundProvider.BitsPerSample || wf.Encoding != WaveFormatEncoding.Pcm)
        {
            throw new InvalidOperationException( $"WAV format mismatch. Expected {SoundProvider.ChannelCount}ch, {SoundProvider.SampleRate}Hz, {SoundProvider.BitsPerSample}-bit PCM. " + $"Got {wf.Channels}ch, {wf.SampleRate}Hz, {wf.BitsPerSample}-bit {wf.Encoding}.");
        }

        WaveFormat = wf;

        long totalSamples = reader.Length / (SoundProvider.BitsPerSample / 8);
        int sampleCount = checked((int)totalSamples);
        var sampleProvider = reader.ToSampleProvider();
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
