using NAudio.Wave;

namespace klooie;
internal sealed class SoundCache
{
    private readonly Dictionary<string, Func<Stream>> factories;
    private readonly Dictionary<string, CachedSound> cached;

    public SoundCache(Dictionary<string, Func<Stream>> rawSoundData)
    {
        factories = new Dictionary<string, Func<Stream>>(rawSoundData, StringComparer.OrdinalIgnoreCase);
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

    public CachedSound(Func<Stream> streamFactory)
    {
        using var stream = streamFactory();
        using var reader = new WaveFileReader(stream);
        WaveFormat = reader.WaveFormat;
        var sampleProvider = reader.ToSampleProvider();

        // Guess a starting size, expand if needed
        int allocSamples = (int)(reader.Length / 4) + 4096; // generous fudge factor
        float[] audioData = new float[allocSamples];

        int totalRead = 0;
        var readBuffer = new float[4096];
        int samplesRead;
        while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            if (totalRead + samplesRead > audioData.Length)
            {
                // Grow array (double in size)
                int newSize = Math.Max(audioData.Length * 2, totalRead + samplesRead);
                Array.Resize(ref audioData, newSize);
            }
            Array.Copy(readBuffer, 0, audioData, totalRead, samplesRead);
            totalRead += samplesRead;
        }

        if (totalRead != audioData.Length)
        {
            Array.Resize(ref audioData, totalRead);
        }

        AudioData = audioData;
        SampleCount = totalRead;
    }
}
