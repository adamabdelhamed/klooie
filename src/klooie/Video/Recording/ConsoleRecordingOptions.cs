namespace klooie;

public sealed class ConsoleRecordingOptions
{
    public DirectoryInfo OutputDirectory { get; set; }
    public TimeSpan ChunkDuration { get; set; } = TimeSpan.FromMinutes(1);
    public RectF? Window { get; set; }
    public Func<TimeSpan> TimestampProvider { get; set; }
    public bool WriteManifest { get; set; } = true;
    public bool CaptureAudio { get; set; } = true;

    internal void Validate()
    {
        if (OutputDirectory == null) throw new ArgumentNullException(nameof(OutputDirectory));
        if (ChunkDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ChunkDuration));
    }
}
