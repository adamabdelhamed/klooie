using NAudio.Wave;

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

    public RecyclableSampleProvider? GetSample(EventLoop eventLoop, string? soundId, VolumeKnob masterVolume, VolumeKnob? sampleVolume, ILifetime? maxLifetime, bool loop)
    {
        if (provider == null) return null;
        if (string.IsNullOrEmpty(soundId)) return null;

        if (!cached.TryGetValue(soundId, out var cachedSound))
        {
            var soundStream = provider.Load(soundId);
            if (soundStream == null) return null;
            cachedSound = new CachedSound(soundStream, soundId);
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

    public string SoundId { get; }

    public CachedSound(Stream stream, string soundId)
    {
        try
        {
            SoundId = soundId;
            using var reader = new WaveFileReader(stream);

            // Guard: Check format
            var wf = reader.WaveFormat;
            if (wf.SampleRate != SoundProvider.SampleRate ||
                wf.Channels != SoundProvider.ChannelCount ||
                wf.BitsPerSample != SoundProvider.BitsPerSample ||
                wf.Encoding != WaveFormatEncoding.Pcm)
            {
                throw new InvalidOperationException(
                    $"WAV format mismatch. Expected {SoundProvider.ChannelCount}ch, {SoundProvider.SampleRate}Hz, {SoundProvider.BitsPerSample}-bit PCM. " +
                    $"Got {wf.Channels}ch, {wf.SampleRate}Hz, {wf.BitsPerSample}-bit {wf.Encoding}.");
            }

            WaveFormat = wf;

            // Calculate total sample count (bytes / 2 bytes per sample / channels)
            long totalSamples = reader.Length / (SoundProvider.BitsPerSample / 8);
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
        finally
        {
            stream.Dispose();
        }
    }
}
