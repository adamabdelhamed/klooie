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

    public RecyclableSampleProvider? GetSample(EventLoop eventLoop, string? soundId, VolumeKnob masterVolume, VolumeKnob? sampleVolume, ILifetime? maxLifetime, bool loop, bool isMusic)
    {
        if (provider == null) return null;
        if (string.IsNullOrEmpty(soundId)) return null;

        if (!cached.TryGetValue(soundId, out var cachedSound))
        {
            if(provider.Contains(soundId) == false)return null;
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
    public float[] AudioData { get; }
    public int SampleCount { get; }
    public WaveFormat WaveFormat { get; }

    public string SoundId { get; }

    public CachedSound(IBinarySoundProvider provider, string soundId)
    {
        SoundId = soundId;
        var parser = new PcmWavParser();
        provider.Load(soundId, (buf, len) => parser.Process(buf.AsSpan(0, len)));
        AudioData = parser.AudioData;
        SampleCount = AudioData.Length;
        WaveFormat = parser.WaveFormat;
    }
}

internal sealed class PcmWavParser
{
    private enum State { Header, Data }
    private State state = State.Header;

    private int fmtBytesNeeded = 0;
    private int dataBytesRemaining = 0;

    public WaveFormat WaveFormat { get; private set; } = null!;
    public float[] AudioData { get; private set; } = Array.Empty<float>();
    private int writePos = 0;

    public void Process(ReadOnlySpan<byte> chunk)
    {
        var span = chunk;

        while (!span.IsEmpty)
        {
            switch (state)
            {
                case State.Header:
                    ParseHeader(ref span);
                    break;
                case State.Data:
                    ParseData(ref span);
                    break;
            }
        }
    }

    private void ParseHeader(ref ReadOnlySpan<byte> span)
    {
        // For simplicity, assume the header fits in the first buffer.
        // You can extend this to be fully incremental if needed.

        var br = new BinaryReader(new MemoryStream(span.ToArray())); // temp: decode header from first chunk

        string riff = new string(br.ReadChars(4));
        if (riff != "RIFF") throw new InvalidDataException("Not RIFF");

        br.ReadInt32(); // riff size
        string wave = new string(br.ReadChars(4));
        if (wave != "WAVE") throw new InvalidDataException("Not WAVE");

        // fmt chunk
        string fmt = new string(br.ReadChars(4));
        int fmtSize = br.ReadInt32();
        short audioFormat = br.ReadInt16();
        short channels = br.ReadInt16();
        int sampleRate = br.ReadInt32();
        int byteRate = br.ReadInt32();
        short blockAlign = br.ReadInt16();
        short bitsPerSample = br.ReadInt16();

        if (audioFormat != 1) throw new NotSupportedException("Only PCM supported");
        if (bitsPerSample != 16 && bitsPerSample != 8)
            throw new NotSupportedException($"Unsupported bits per sample: {bitsPerSample}");

        if (fmtSize > 16)
            br.ReadBytes(fmtSize - 16);

        // Find "data" chunk
        string dataId;
        int dataSize;
        do
        {
            dataId = new string(br.ReadChars(4));
            dataSize = br.ReadInt32();
            if (dataId != "data")
                br.BaseStream.Seek(dataSize, SeekOrigin.Current);
        } while (dataId != "data");

        dataBytesRemaining = dataSize;
        AudioData = new float[dataSize / (bitsPerSample / 8)];
        writePos = 0;

        WaveFormat = WaveFormat.CreateCustomFormat(
            WaveFormatEncoding.Pcm, sampleRate, channels, byteRate, blockAlign, bitsPerSample);

        // Jump parser to data state
        state = State.Data;

        // Consume the bytes used
        span = span.Slice((int)br.BaseStream.Position);
    }

    private void ParseData(ref ReadOnlySpan<byte> span)
    {
        int bytesPerSample = WaveFormat.BitsPerSample / 8;
        int samplesThisChunk = Math.Min(span.Length / bytesPerSample, dataBytesRemaining / bytesPerSample);

        if (WaveFormat.BitsPerSample == 16)
        {
            for (int i = 0; i < samplesThisChunk; i++)
            {
                short sample = BitConverter.ToInt16(span.Slice(i * 2, 2));
                AudioData[writePos++] = sample / 32768f;
            }
        }
        else if (WaveFormat.BitsPerSample == 8)
        {
            for (int i = 0; i < samplesThisChunk; i++)
            {
                byte sample = span[i];
                AudioData[writePos++] = (sample - 128) / 128f;
            }
        }

        int bytesConsumed = samplesThisChunk * bytesPerSample;
        span = span.Slice(bytesConsumed);
        dataBytesRemaining -= bytesConsumed;

        if (dataBytesRemaining == 0)
        {
            state = State.Header; // finished
        }
    }
}
