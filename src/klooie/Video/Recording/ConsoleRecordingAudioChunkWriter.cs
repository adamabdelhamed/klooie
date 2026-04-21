using System.Buffers;

namespace klooie;

public sealed class ConsoleRecordingAudioChunkInfo
{
    public int ChunkIndex { get; set; }
    public string AudioPath { get; set; } = "";
    public long FirstAudioSampleFrame { get; set; } = -1;
    public long AudioSampleCount { get; set; }
    public int AudioSampleRate { get; set; }
    public int AudioChannels { get; set; }
}

internal sealed class ConsoleRecordingAudioChunkWriter : IDisposable
{
    private const short FormatPcm = 1;
    private const short BitsPerSample = 16;
    private readonly FileInfo tempFile;
    private readonly FileInfo finalFile;
    private readonly int chunkIndex;
    private readonly int sampleRate;
    private readonly int channels;
    private readonly FileStream stream;
    private bool finished;
    private long sampleCount;
    private long firstSampleFrame = -1;

    public int ChunkIndex => chunkIndex;
    public long SampleCount => sampleCount;
    public int SampleRate => sampleRate;
    public int Channels => channels;

    public ConsoleRecordingAudioChunkWriter(FileInfo tempFile, FileInfo finalFile, int chunkIndex, int sampleRate, int channels)
    {
        this.tempFile = tempFile ?? throw new ArgumentNullException(nameof(tempFile));
        this.finalFile = finalFile ?? throw new ArgumentNullException(nameof(finalFile));
        this.chunkIndex = chunkIndex;
        this.sampleRate = sampleRate;
        this.channels = channels;
        tempFile.Directory?.Create();
        stream = new FileStream(tempFile.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        WriteHeader(0);
    }

    public void WriteSamples(ReadOnlySpan<float> samples, long firstSampleFrame)
    {
        if (finished) throw new InvalidOperationException("Audio chunk writer has already been finished");
        if (samples.Length == 0) return;
        if (this.firstSampleFrame < 0) this.firstSampleFrame = firstSampleFrame;

        var byteCount = checked(samples.Length * 2);
        var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var bytes = buffer.AsSpan(0, byteCount);
            for (var i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                if (sample > 1f) sample = 1f;
                else if (sample < -1f) sample = -1f;

                var pcm = (short)Math.Round(sample * short.MaxValue);
                bytes[i * 2] = (byte)(pcm & 0xff);
                bytes[i * 2 + 1] = (byte)((pcm >> 8) & 0xff);
            }

            stream.Write(bytes);
            sampleCount += samples.Length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ConsoleRecordingAudioChunkInfo Finish()
    {
        if (finished) throw new InvalidOperationException("Audio chunk writer has already been finished");
        finished = true;
        var dataBytes = checked(sampleCount * 2);
        stream.Position = 0;
        WriteHeader(dataBytes);
        stream.Flush(true);
        stream.Dispose();

        if (finalFile.Exists) finalFile.Delete();
        File.Move(tempFile.FullName, finalFile.FullName);

        return new ConsoleRecordingAudioChunkInfo
        {
            ChunkIndex = chunkIndex,
            AudioPath = Path.Combine("audio", finalFile.Name),
            FirstAudioSampleFrame = firstSampleFrame,
            AudioSampleCount = sampleCount,
            AudioSampleRate = sampleRate,
            AudioChannels = channels,
        };
    }

    public void Dispose()
    {
        if (finished) return;
        stream.Dispose();
        try { if (tempFile.Exists) tempFile.Delete(); } catch { }
    }

    private void WriteHeader(long dataBytes)
    {
        var byteRate = sampleRate * channels * BitsPerSample / 8;
        var blockAlign = (short)(channels * BitsPerSample / 8);

        WriteAscii("RIFF");
        WriteInt32(checked((int)(36 + dataBytes)));
        WriteAscii("WAVE");
        WriteAscii("fmt ");
        WriteInt32(16);
        WriteInt16(FormatPcm);
        WriteInt16((short)channels);
        WriteInt32(sampleRate);
        WriteInt32(byteRate);
        WriteInt16(blockAlign);
        WriteInt16(BitsPerSample);
        WriteAscii("data");
        WriteInt32(checked((int)dataBytes));
    }

    private void WriteAscii(string value)
    {
        for (var i = 0; i < value.Length; i++) stream.WriteByte((byte)value[i]);
    }

    private void WriteInt16(short value)
    {
        stream.WriteByte((byte)(value & 0xff));
        stream.WriteByte((byte)((value >> 8) & 0xff));
    }

    private void WriteInt32(int value)
    {
        stream.WriteByte((byte)(value & 0xff));
        stream.WriteByte((byte)((value >> 8) & 0xff));
        stream.WriteByte((byte)((value >> 16) & 0xff));
        stream.WriteByte((byte)((value >> 24) & 0xff));
    }
}
