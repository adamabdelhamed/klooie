using NAudio.Wave;

namespace klooie;
internal sealed class SoundCache
{
    private readonly Dictionary<string, CachedSound> soundCacheDictionary;

    public SoundCache(Dictionary<string, Func<Stream>> rawSoundData)
    {
        soundCacheDictionary = rawSoundData.ToDictionary(kvp => kvp.Key, kvp => new CachedSound(kvp.Value), StringComparer.OrdinalIgnoreCase);
        GC.Collect();
    }

    public bool TryCreate(EventLoop eventLoop, string soundId, float volume, ILifetime? maxLifetime, bool loop, out RecyclableSampleProvider ret)
    {
        if(soundCacheDictionary.TryGetValue(soundId, out var cachedSound) == false)
        {
            ret = null!;
            return false;
        }
        
        ret = RecyclableSampleProvider.Create(eventLoop, cachedSound, volume, maxLifetime, loop);
        return true;
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
