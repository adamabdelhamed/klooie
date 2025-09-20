using NAudio.Wave;
using System.Text;

namespace klooie;
internal sealed class SoundCache
{
    private readonly IBinarySoundProvider? provider;
    private readonly Dictionary<string, CachedSound> cached;

    // Single, reusable buffer for music (only one song plays at a time).
    private CachedSound musicReusable = CachedSound.ReusableMusicSound();

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
            musicReusable.LoadFrom(provider, soundId, i => throw new InvalidOperationException("Music track is too large to fit in the reusable buffer."));
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
        LoadFrom(provider, soundId, required => required); // exact size for SFX
    }


    private CachedSound() { }

    private const long LengthOfArrayFor5MinutesOfMusic = SoundProvider.SampleRate * SoundProvider.ChannelCount * 60 * 5; 

    public static CachedSound ReusableMusicSound()
    {
        return new CachedSound
        {
            AudioData = new float[LengthOfArrayFor5MinutesOfMusic],
            SampleCount = 0,
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount),
            SoundId = string.Empty
        };
    }

    /// <summary>
    /// Loads (or reloads) audio data from the provider into this instance.
    /// If the current capacity is insufficient, the buffer is grown according to <paramref name="growPolicy"/>.
    /// </summary>
    public void LoadFrom(IBinarySoundProvider provider, string soundId, Func<int, int> growPolicy)
    {
        SoundId = soundId;

        // Parse WAV incrementally, reusing/growing our buffer as needed.
        var parser = new PcmWavParser(
            reusableBuffer: AudioData,
            growPolicy: growPolicy);

        provider.Load(soundId, (buf, len) => parser.Process(buf.AsSpan(0, len)));

        // Commit parsed results
        AudioData = parser.AudioData;              // possibly reused or grown
        SampleCount = parser.SamplesWritten;       // number of valid samples
        WaveFormat = parser.WaveFormat;            // parsed format
    }
}

/// <summary>
/// Simple incremental PCM WAV parser that writes directly into a reusable float[] buffer.
/// Supports 8-bit and 16-bit PCM. Grows the buffer using a provided policy when needed.
/// </summary>
internal sealed class PcmWavParser
{
    private enum State { Header, Data }
    private State state = State.Header;

    private int dataBytesRemaining = 0;
    private int bytesPerSample = 0;

    private readonly Func<int, int> growPolicy;
    private float[] buffer;
    private int writePos = 0;

    public WaveFormat WaveFormat { get; private set; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); // replaced after header
    public float[] AudioData => buffer;
    public int SamplesWritten => writePos;

    public PcmWavParser(float[]? reusableBuffer, Func<int, int> growPolicy)
    {
        this.buffer = reusableBuffer ?? Array.Empty<float>();
        this.growPolicy = growPolicy ?? (i => i);
    }

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
        // For simplicity, assume the entire header is in this first chunk.
        // (Our provider typically hands us the file in large pieces.)
        // If needed, this can be made fully incremental later.

        using var ms = new MemoryStream(span.ToArray(), writable: false);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        string riff = new string(br.ReadChars(4));
        if (riff != "RIFF") throw new InvalidDataException("Not RIFF");

        br.ReadInt32(); // riff size
        string wave = new string(br.ReadChars(4));
        if (wave != "WAVE") throw new InvalidDataException("Not WAVE");

        // fmt chunk (may have extra bytes)
        string fmt = new string(br.ReadChars(4));
        if (fmt != "fmt ") throw new InvalidDataException("Missing fmt chunk");
        int fmtSize = br.ReadInt32();
        short audioFormat = br.ReadInt16();          // 1 = PCM
        short channels = br.ReadInt16();
        int sampleRate = br.ReadInt32();
        int byteRate = br.ReadInt32();
        short blockAlign = br.ReadInt16();
        short bitsPerSample = br.ReadInt16();

        if (audioFormat != 1) throw new NotSupportedException("Only PCM supported");
        if (bitsPerSample != 16 && bitsPerSample != 8)
            throw new NotSupportedException($"Unsupported bits per sample: {bitsPerSample}");

        if (fmtSize > 16)
            br.ReadBytes(fmtSize - 16); // skip any extra fmt bytes

        // Find "data" chunk (skip other chunks if present)
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
        bytesPerSample = bitsPerSample / 8;

        // Compute number of samples in the file (across all channels)
        int samplesNeeded = dataSize / bytesPerSample;

        // Ensure capacity: reuse if large enough, otherwise grow by policy.
        if (buffer.Length < samplesNeeded)
            buffer = new float[growPolicy(samplesNeeded)];

        writePos = 0;

        WaveFormat = WaveFormat.CreateCustomFormat(
            WaveFormatEncoding.Pcm, sampleRate, channels, byteRate, blockAlign, bitsPerSample);

        // Advance outer span to after the header we've just consumed
        span = span.Slice((int)br.BaseStream.Position);
        state = State.Data;
    }

    private void ParseData(ref ReadOnlySpan<byte> span)
    {
        // Determine how many samples are in this chunk and still remaining in the file.
        int chunkSamplesAvailable = Math.Min(span.Length / bytesPerSample, dataBytesRemaining / bytesPerSample);

        // Ensure capacity for this write segment (grow if needed)
        int requiredTotal = writePos + chunkSamplesAvailable;
        if (requiredTotal > buffer.Length)
        {
            int newCap = growPolicy(requiredTotal);
            if (newCap < requiredTotal) newCap = requiredTotal; // safety
            Array.Resize(ref buffer, newCap);
        }

        if (bytesPerSample == 2) // 16-bit PCM
        {
            // Fast path: walk span in 2-byte steps
            for (int i = 0; i < chunkSamplesAvailable; i++)
            {
                short sample = BitConverter.ToInt16(span.Slice(i * 2, 2));
                buffer[writePos++] = sample / 32768f;
            }
        }
        else // 8-bit PCM
        {
            for (int i = 0; i < chunkSamplesAvailable; i++)
            {
                byte sample = span[i];
                buffer[writePos++] = (sample - 128) / 128f;
            }
        }

        int bytesConsumed = chunkSamplesAvailable * bytesPerSample;
        span = span.Slice(bytesConsumed);
        dataBytesRemaining -= bytesConsumed;

        if (dataBytesRemaining == 0)
        {
            state = State.Header; // finished file
        }
    }
}