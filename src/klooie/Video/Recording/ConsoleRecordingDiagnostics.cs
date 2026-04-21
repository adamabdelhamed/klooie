namespace klooie;

public sealed class ConsoleRecordingDiagnostics
{
    public long FrameCount { get; internal set; }
    public long RawFrameCount { get; internal set; }
    public long DiffFrameCount { get; internal set; }
    public long BytesWritten { get; internal set; }
    public TimeSpan MaxWriteDuration { get; internal set; }
    public TimeSpan AverageWriteDuration { get; internal set; }
    public TimeSpan WriterLag { get; internal set; }
    public long DroppedFrameCount { get; internal set; }
    public long DroppedAudioCount { get; internal set; }
    public long AudioSampleCount { get; internal set; }
    public long ChunkFinalizationCount { get; internal set; }
    public string LastError { get; internal set; }

    internal ConsoleRecordingDiagnostics Clone() => new ConsoleRecordingDiagnostics
    {
        FrameCount = FrameCount,
        RawFrameCount = RawFrameCount,
        DiffFrameCount = DiffFrameCount,
        BytesWritten = BytesWritten,
        MaxWriteDuration = MaxWriteDuration,
        AverageWriteDuration = AverageWriteDuration,
        WriterLag = WriterLag,
        DroppedFrameCount = DroppedFrameCount,
        DroppedAudioCount = DroppedAudioCount,
        AudioSampleCount = AudioSampleCount,
        ChunkFinalizationCount = ChunkFinalizationCount,
        LastError = LastError,
    };
}

internal sealed class ConsoleRecordingDiagnosticsBuilder
{
    private readonly ConsoleRecordingDiagnostics data = new ConsoleRecordingDiagnostics();
    private long totalWriteTicks;

    public ConsoleRecordingDiagnostics Snapshot => data.Clone();

    public void RecordFrame(ConsoleVideoFrameKind kind, TimeSpan writeDuration, long bytesWritten)
    {
        data.FrameCount++;
        if (kind == ConsoleVideoFrameKind.Raw) data.RawFrameCount++;
        else data.DiffFrameCount++;

        data.BytesWritten += bytesWritten;
        if (writeDuration > data.MaxWriteDuration) data.MaxWriteDuration = writeDuration;
        totalWriteTicks += writeDuration.Ticks;
        data.AverageWriteDuration = data.FrameCount == 0 ? TimeSpan.Zero : new TimeSpan(totalWriteTicks / data.FrameCount);
    }

    public void RecordChunkFinalized() => data.ChunkFinalizationCount++;
    public void RecordWriterLag(TimeSpan lag) => data.WriterLag = lag;
    public void RecordDroppedFrame() => data.DroppedFrameCount++;
    public void RecordDroppedAudio() => data.DroppedAudioCount++;
    public void RecordAudioSamples(long sampleCount, long bytesWritten)
    {
        data.AudioSampleCount += sampleCount;
        data.BytesWritten += bytesWritten;
    }
    public void RecordError(Exception ex) => data.LastError = ex.ToString();
}
