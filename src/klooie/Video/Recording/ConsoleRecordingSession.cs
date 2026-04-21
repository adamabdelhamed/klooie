using System.Diagnostics;

namespace klooie;

public sealed class ConsoleRecordingSession : IDisposable, IConsoleAudioRecordingSink
{
    private readonly object sync = new object();
    private readonly ConsoleControl target;
    private readonly ConsoleRecordingOptions options;
    private readonly ConsoleRecordingDiagnosticsBuilder diagnostics = new ConsoleRecordingDiagnosticsBuilder();
    private readonly ConsoleRecordingManifest manifest;
    private readonly Dictionary<int, ConsoleRecordingAudioChunkWriter> audioChunks = new Dictionary<int, ConsoleRecordingAudioChunkWriter>();
    private readonly Dictionary<int, ConsoleRecordingAudioChunkInfo> finalizedAudioChunks = new Dictionary<int, ConsoleRecordingAudioChunkInfo>();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private ConsoleVideoChunkWriter currentChunk;
    private TimeSpan currentChunkStart = TimeSpan.Zero;
    private long? audioBaseSampleFrame;
    private TimeSpan audioBaseSessionTime;
    private bool stopped;
    private bool disposed;

    public ConsoleRecordingDiagnostics Diagnostics => diagnostics.Snapshot;
    public DirectoryInfo OutputDirectory => options.OutputDirectory;

    private ConsoleRecordingSession(ConsoleControl target, ConsoleRecordingOptions options)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        options.OutputDirectory.Create();
        GetChunksDirectory(options.OutputDirectory).Create();
        GetAudioDirectory(options.OutputDirectory).Create();
        manifest = new ConsoleRecordingManifest { ChunkDurationTicks = options.ChunkDuration.Ticks };
    }

    public static ConsoleRecordingSession Start(ConsoleControl target, ConsoleRecordingOptions options)
    {
        var session = new ConsoleRecordingSession(target, options);
        target.EnableRecording(session);
        if (options.CaptureAudio && SoundProvider.Current != null)
        {
            SoundProvider.Current.AudioRecordingSink = session;
        }
        target.OnDisposed(session, static session => session.Stop());
        return session;
    }

    public void WriteFrame(ConsoleBitmap bitmap)
    {
        if (stopped || disposed) return;

        try
        {
            lock (sync)
            {
                var timestamp = GetTimestamp();
                if (currentChunk == null || timestamp - currentChunkStart >= options.ChunkDuration)
                {
                    TryFinishCurrentChunk(timestamp);
                    StartChunk(timestamp);
                    FinishAudioChunksBefore(GetChunkIndex(timestamp));
                }

                currentChunk.WriteFrame(bitmap, timestamp, currentChunk.FrameCount == 0);
            }
        }
        catch (Exception ex)
        {
            diagnostics.RecordError(ex);
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        if (stopped) return;
        stopped = true;
        lock (sync)
        {
            TryFinishCurrentChunk(GetTimestamp());
            FinishAllAudioChunks();
            WriteManifestIfNeeded();
        }

        if (SoundProvider.Current?.AudioRecordingSink == this)
        {
            SoundProvider.Current.AudioRecordingSink = null;
        }
        target.ClearRecordingSession(this);
    }

    public bool TryFinishCurrentChunk()
    {
        lock (sync) return TryFinishCurrentChunk(GetTimestamp());
    }

    public void WriteAudioSamples(ReadOnlySpan<float> samples, int sampleRate, int channels, long firstSampleFrame)
    {
        if (stopped || disposed || options.CaptureAudio == false) return;
        if (samples.Length == 0 || sampleRate <= 0 || channels <= 0) return;

        try
        {
            lock (sync)
            {
                if (audioBaseSampleFrame.HasValue == false)
                {
                    audioBaseSampleFrame = firstSampleFrame;
                    audioBaseSessionTime = GetTimestamp();
                }

                var sampleOffset = 0;
                var currentSampleFrame = firstSampleFrame;
                while (sampleOffset < samples.Length)
                {
                    var sampleTime = GetSessionTimeForAudioSample(currentSampleFrame, sampleRate);
                    var chunkIndex = GetChunkIndex(sampleTime);
                    var chunkStart = GetChunkStart(chunkIndex);
                    var nextChunkStart = chunkStart + options.ChunkDuration;
                    var framesUntilChunkEnd = Math.Max(1, (long)Math.Ceiling((nextChunkStart - sampleTime).TotalSeconds * sampleRate));
                    var remainingFrames = (samples.Length - sampleOffset) / channels;
                    var framesToWrite = Math.Min(remainingFrames, framesUntilChunkEnd);
                    var sampleCount = checked((int)(framesToWrite * channels));

                    var writer = GetAudioChunkWriter(chunkIndex, chunkStart, sampleRate, channels);
                    writer.WriteSamples(samples.Slice(sampleOffset, sampleCount), currentSampleFrame);
                    diagnostics.RecordAudioSamples(sampleCount, sampleCount * 2L);

                    sampleOffset += sampleCount;
                    currentSampleFrame += framesToWrite;
                }

                FinishAudioChunksBefore(GetChunkIndex(GetTimestamp()));
            }
        }
        catch (Exception ex)
        {
            diagnostics.RecordDroppedAudio();
            diagnostics.RecordError(ex);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
    }

    public static DirectoryInfo GetChunksDirectory(DirectoryInfo sessionDirectory) => new DirectoryInfo(Path.Combine(sessionDirectory.FullName, "chunks"));
    public static DirectoryInfo GetAudioDirectory(DirectoryInfo sessionDirectory) => new DirectoryInfo(Path.Combine(sessionDirectory.FullName, "audio"));

    private TimeSpan GetTimestamp() => options.TimestampProvider?.Invoke() ?? clock.Elapsed;
    private int GetChunkIndex(TimeSpan timestamp) => Math.Max(0, (int)(timestamp.Ticks / options.ChunkDuration.Ticks));
    private TimeSpan GetChunkStart(int chunkIndex) => TimeSpan.FromTicks(chunkIndex * options.ChunkDuration.Ticks);

    private void StartChunk(TimeSpan timestamp)
    {
        var chunkIndex = GetChunkIndex(timestamp);
        currentChunkStart = GetChunkStart(chunkIndex);
        var chunksDirectory = GetChunksDirectory(options.OutputDirectory);
        var name = $"chunk-{chunkIndex:D6}.cv";
        var temp = new FileInfo(Path.Combine(chunksDirectory.FullName, name + ".tmp"));
        var final = new FileInfo(Path.Combine(chunksDirectory.FullName, name));
        currentChunk = new ConsoleVideoChunkWriter(temp, final, chunkIndex, currentChunkStart, options.Window, diagnostics);
    }

    private bool TryFinishCurrentChunk(TimeSpan now)
    {
        if (currentChunk == null) return false;

        var chunk = currentChunk;
        currentChunk = null;

        if (chunk.FrameCount == 0)
        {
            chunk.Dispose();
            return false;
        }

        var duration = now - chunk.ChunkStart;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        var info = chunk.Finish(duration);
        ApplyAudioInfo(info);
        manifest.Chunks.Add(info);
        WriteManifestIfNeeded();
        return true;
    }

    private TimeSpan GetSessionTimeForAudioSample(long sampleFrame, int sampleRate)
    {
        var baseFrame = audioBaseSampleFrame ?? sampleFrame;
        var deltaFrames = sampleFrame - baseFrame;
        return audioBaseSessionTime + TimeSpan.FromSeconds(deltaFrames / (double)sampleRate);
    }

    private ConsoleRecordingAudioChunkWriter GetAudioChunkWriter(int chunkIndex, TimeSpan chunkStart, int sampleRate, int channels)
    {
        if (audioChunks.TryGetValue(chunkIndex, out var writer)) return writer;

        var audioDirectory = GetAudioDirectory(options.OutputDirectory);
        var name = $"chunk-{chunkIndex:D6}.wav";
        var temp = new FileInfo(Path.Combine(audioDirectory.FullName, name + ".tmp"));
        var final = new FileInfo(Path.Combine(audioDirectory.FullName, name));
        writer = new ConsoleRecordingAudioChunkWriter(temp, final, chunkIndex, sampleRate, channels);
        audioChunks.Add(chunkIndex, writer);
        return writer;
    }

    private void FinishAudioChunksBefore(int chunkIndex)
    {
        var toFinish = audioChunks.Keys.Where(k => k < chunkIndex).OrderBy(k => k).ToArray();
        for (var i = 0; i < toFinish.Length; i++)
        {
            FinishAudioChunk(toFinish[i]);
        }
    }

    private void FinishAllAudioChunks()
    {
        var toFinish = audioChunks.Keys.OrderBy(k => k).ToArray();
        for (var i = 0; i < toFinish.Length; i++)
        {
            FinishAudioChunk(toFinish[i]);
        }
    }

    private void FinishAudioChunk(int chunkIndex)
    {
        if (audioChunks.TryGetValue(chunkIndex, out var writer) == false) return;
        audioChunks.Remove(chunkIndex);
        if (writer.SampleCount == 0)
        {
            writer.Dispose();
            return;
        }

        finalizedAudioChunks[chunkIndex] = writer.Finish();
        for (var i = 0; i < manifest.Chunks.Count; i++)
        {
            if (manifest.Chunks[i].ChunkIndex == chunkIndex) ApplyAudioInfo(manifest.Chunks[i]);
        }
        WriteManifestIfNeeded();
    }

    private void ApplyAudioInfo(ConsoleRecordingChunkInfo chunk)
    {
        if (finalizedAudioChunks.TryGetValue(chunk.ChunkIndex, out var audio) == false) return;
        chunk.AudioPath = audio.AudioPath;
        chunk.FirstAudioSampleFrame = audio.FirstAudioSampleFrame;
        chunk.AudioSampleCount = audio.AudioSampleCount;
        chunk.AudioSampleRate = audio.AudioSampleRate;
        chunk.AudioChannels = audio.AudioChannels;
    }

    private void WriteManifestIfNeeded()
    {
        if (options.WriteManifest) ConsoleRecordingManifestStore.Write(options.OutputDirectory, manifest);
    }
}
