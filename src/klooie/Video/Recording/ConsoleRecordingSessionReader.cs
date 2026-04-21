namespace klooie;

public sealed class ConsoleRecordingSessionReader
{
    private readonly DirectoryInfo sessionDirectory;
    private readonly FileInfo manifestFile;
    private readonly ConsoleRecordingManifest manifest;

    public DirectoryInfo SessionDirectory => sessionDirectory;
    public FileInfo ManifestFile => manifestFile;
    public ConsoleRecordingManifest Manifest => manifest;
    public TimeSpan Duration { get; }

    public ConsoleRecordingSessionReader(DirectoryInfo sessionDirectory)
        : this(ConsoleRecordingManifestStore.GetManifestFile(sessionDirectory), sessionDirectory)
    {
    }

    public ConsoleRecordingSessionReader(FileInfo manifestFile)
        : this(manifestFile, manifestFile?.Directory)
    {
    }

    private ConsoleRecordingSessionReader(FileInfo manifestFile, DirectoryInfo sessionDirectory)
    {
        this.manifestFile = manifestFile ?? throw new ArgumentNullException(nameof(manifestFile));
        this.sessionDirectory = sessionDirectory ?? throw new ArgumentException("Recording manifest must be inside a session directory", nameof(manifestFile));
        if (this.sessionDirectory.Exists == false) throw new DirectoryNotFoundException(this.sessionDirectory.FullName);
        if (ConsoleRecordingManifestStore.IsManifestFile(manifestFile) == false) throw new FormatException($"Recording manifest files must use the {ConsoleRecordingManifestStore.ManifestExtension} extension");

        manifest = ConsoleRecordingManifestStore.RebuildFromChunks(this.sessionDirectory);
        if (manifest.Chunks.Count == 0) throw new FormatException("Recording session contains no finalized video chunks");
        Duration = new TimeSpan(manifest.Chunks.Max(c => c.ChunkStartTicks + c.DurationTicks));
    }

    public InMemoryConsoleBitmapVideo ReadToEnd(Action<InMemoryConsoleBitmapVideo> progressCallback = null)
    {
        var ret = new InMemoryConsoleBitmapVideo
        {
            Duration = Duration,
        };

        foreach (var chunk in manifest.Chunks.OrderBy(c => c.ChunkIndex))
        {
            var chunkFile = ResolveChunkFile(chunk);
            using var reader = new ConsoleVideoChunkReader(chunkFile);
            while (reader.ReadFrame())
            {
                ret.Frames.Add(new InMemoryConsoleBitmapFrame
                {
                    Bitmap = reader.CurrentBitmap.Clone(),
                    FrameTime = reader.CurrentTimestamp,
                });

                ret.LoadProgress = Duration.Ticks <= 0 ? 1 : Math.Min(1, reader.CurrentTimestamp.TotalSeconds / Duration.TotalSeconds);
                progressCallback?.Invoke(ret);
            }
        }

        ret.LoadProgress = 1;
        progressCallback?.Invoke(ret);
        return ret;
    }

    public FileInfo ResolveChunkFile(ConsoleRecordingChunkInfo chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk.VideoPath) == false)
        {
            var relative = chunk.VideoPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var file = new FileInfo(Path.Combine(sessionDirectory.FullName, relative));
            if (file.Exists) return file;
        }

        return new FileInfo(Path.Combine(ConsoleRecordingSession.GetChunksDirectory(sessionDirectory).FullName, $"chunk-{chunk.ChunkIndex:D6}.cv"));
    }

    public FileInfo ResolveAudioFile(ConsoleRecordingChunkInfo chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk.AudioPath) == false)
        {
            var relative = chunk.AudioPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return new FileInfo(Path.Combine(sessionDirectory.FullName, relative));
        }

        return new FileInfo(Path.Combine(ConsoleRecordingSession.GetAudioDirectory(sessionDirectory).FullName, $"chunk-{chunk.ChunkIndex:D6}.wav"));
    }
}
