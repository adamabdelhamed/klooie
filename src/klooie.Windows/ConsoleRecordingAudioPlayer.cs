using NAudio.Wave;

namespace klooie;

public sealed class ConsoleRecordingAudioPlayer : IConsoleRecordingAudioPlayback
{
    private readonly object sync = new object();
    private ConsoleRecordingSessionReader? sessionReader;
    private List<ConsoleRecordingChunkInfo> chunks = new List<ConsoleRecordingChunkInfo>();
    private WaveOutEvent? output;
    private AudioFileReader? reader;
    private int currentChunkIndex = -1;
    private bool disposed;
    private bool intentionalStop;

    public void Load(ConsoleRecordingSessionReader sessionReader)
    {
        if (sessionReader == null) throw new ArgumentNullException(nameof(sessionReader));
        lock (sync)
        {
            this.sessionReader = sessionReader;
            chunks = sessionReader.Manifest.Chunks
                .Where(c => string.IsNullOrWhiteSpace(c.AudioPath) == false)
                .OrderBy(c => c.ChunkIndex)
                .ToList();
        }
    }

    public void PlayFrom(TimeSpan position)
    {
        lock (sync)
        {
            if (disposed || sessionReader == null || chunks.Count == 0) return;
            StopCurrentLocked();

            var chunk = FindChunk(position);
            if (chunk == null) return;

            var file = ResolveAudioFile(chunk);
            if (file.Exists == false) return;

            reader = new AudioFileReader(file.FullName);
            var offset = position - TimeSpan.FromTicks(chunk.ChunkStartTicks);
            if (offset < TimeSpan.Zero) offset = TimeSpan.Zero;
            if (offset < reader.TotalTime) reader.CurrentTime = offset;

            currentChunkIndex = chunks.IndexOf(chunk);
            output = new WaveOutEvent();
            output.PlaybackStopped += OnPlaybackStopped;
            intentionalStop = false;
            output.Init(reader);
            output.Play();
        }
    }

    public void Pause()
    {
        lock (sync)
        {
            if (output?.PlaybackState == PlaybackState.Playing) output.Pause();
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            StopCurrentLocked();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            StopCurrentLocked();
        }
    }

    private ConsoleRecordingChunkInfo? FindChunk(TimeSpan position)
    {
        ConsoleRecordingChunkInfo? best = null;
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (TimeSpan.FromTicks(chunk.ChunkStartTicks) <= position) best = chunk;
            var chunkEnd = TimeSpan.FromTicks(chunk.ChunkStartTicks + chunk.DurationTicks);
            if (position >= TimeSpan.FromTicks(chunk.ChunkStartTicks) && position < chunkEnd) return chunk;
        }

        return best;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (sync)
        {
            if (disposed || intentionalStop) return;
            DisposeCurrentLocked();

            currentChunkIndex++;
            if (currentChunkIndex < 0 || currentChunkIndex >= chunks.Count) return;
            var next = chunks[currentChunkIndex];
            var file = ResolveAudioFile(next);
            if (file.Exists == false) return;

            reader = new AudioFileReader(file.FullName);
            output = new WaveOutEvent();
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(reader);
            output.Play();
        }
    }

    private FileInfo ResolveAudioFile(ConsoleRecordingChunkInfo chunk)
    {
        var relative = chunk.AudioPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return new FileInfo(Path.Combine(sessionReader!.SessionDirectory.FullName, relative));
    }

    private void StopCurrentLocked()
    {
        intentionalStop = true;
        if (output != null)
        {
            try { output.Stop(); } catch { }
        }

        DisposeCurrentLocked();
        currentChunkIndex = -1;
        intentionalStop = false;
    }

    private void DisposeCurrentLocked()
    {
        if (output != null)
        {
            output.PlaybackStopped -= OnPlaybackStopped;
            try { output.Dispose(); } catch { }
            output = null;
        }

        if (reader != null)
        {
            try { reader.Dispose(); } catch { }
            reader = null;
        }
    }
}
