using NAudio.Wave;

namespace klooie;
internal sealed class SoundCache
{
    private readonly Dictionary<string, Func<Stream>> factories;
    private readonly Dictionary<string, CachedSound> cached;

    public SoundCache(Dictionary<string, Func<Stream>> factories)
    {
        this.factories = factories;
        cached = new Dictionary<string, CachedSound>(StringComparer.OrdinalIgnoreCase);
    }

    public RecyclableSampleProvider? GetSample(EventLoop eventLoop, string? soundId, VolumeKnob masterVolume, VolumeKnob? sampleVolume, ILifetime? maxLifetime, bool loop)
    {
        if (string.IsNullOrEmpty(soundId)) return null;
        if (!factories.TryGetValue(soundId, out var factory)) return null;

        if (!cached.TryGetValue(soundId, out var cachedSound))
        {
            cachedSound = new CachedSound(factory);
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
    public float[] AudioData { get; }
    public int SampleCount { get; }
    public WaveFormat WaveFormat { get; }

    private const int ExpectedSampleRate = 44100;
    private const int ExpectedChannels = 2;
    private const int ExpectedBitsPerSample = 16;

    public CachedSound(Func<Stream> streamFactory)
    {
        using var stream = streamFactory();
        using var reader = new WaveFileReader(stream);

        // Guard: Check format
        var wf = reader.WaveFormat;
        if (wf.SampleRate != ExpectedSampleRate ||
            wf.Channels != ExpectedChannels ||
            wf.BitsPerSample != ExpectedBitsPerSample ||
            wf.Encoding != WaveFormatEncoding.Pcm)
        {
            throw new InvalidOperationException(
                $"WAV format mismatch. Expected {ExpectedChannels}ch, {ExpectedSampleRate}Hz, {ExpectedBitsPerSample}-bit PCM. " +
                $"Got {wf.Channels}ch, {wf.SampleRate}Hz, {wf.BitsPerSample}-bit {wf.Encoding}.");
        }

        WaveFormat = wf;

        // Calculate total sample count (bytes / 2 bytes per sample / channels)
        long totalSamples = reader.Length / (ExpectedBitsPerSample / 8);
        int sampleCount = checked((int)totalSamples);

        // NAudio provides ToSampleProvider() for float conversion
        var sampleProvider = reader.ToSampleProvider();

        // Pre-allocate float[]: NAudio's sample provider gives floats per channel
        float[] audioData = new float[sampleCount];

        int totalRead = 0;
        while (totalRead < sampleCount)
        {
            int read = sampleProvider.Read(audioData, totalRead, sampleCount - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        if (totalRead != sampleCount)
        {
            throw new InvalidOperationException(
                $"Expected to read {sampleCount} samples, but only read {totalRead}. " +
                "This may indicate an issue with the WAV file or its format.");
        }

        AudioData = audioData;
        SampleCount = totalRead;
    }
}
