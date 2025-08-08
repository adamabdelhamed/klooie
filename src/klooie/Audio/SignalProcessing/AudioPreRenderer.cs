using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace klooie;

#region Keys & DTOs
/// <summary>Unique identity of a note for caching purposes.</summary>
public readonly struct NoteKey : IEquatable<NoteKey>
{
    public readonly int Midi;
    public readonly int Velocity;
    public readonly double DurationBeats;
    public readonly string InstrumentName;

 
    public NoteKey(NoteExpression n)
    {
        Midi = n.MidiNote;
        Velocity = n.Velocity;
        DurationBeats = Math.Round(n.DurationBeats,6);
        InstrumentName = n.Instrument?.Name ?? "null";
    }

    public bool Equals(NoteKey other) =>
        Midi == other.Midi &&
        Velocity == other.Velocity &&
        DurationBeats.Equals(other.DurationBeats) &&
        InstrumentName == other.InstrumentName;

    public override bool Equals(object? obj) => obj is NoteKey k && Equals(k);

    public override int GetHashCode()
        => HashCode.Combine(Midi, Velocity, DurationBeats, InstrumentName);

    public override string ToString()
    {
        return $"NoteKey(Midi={Midi}, Velocity={Velocity}, DurationSec={DurationBeats:F2}, Instrument='{InstrumentName}')";
    }
}

public sealed class CachedWave
{
    public readonly float[] Data;   // interleaved, channels = SoundProvider.ChannelCount
    public readonly int Frames; // samples per channel

    public int Bytes => sizeof(float) * Data.Length;

    public CachedWave(float[] data, int frames)
    {
        Data = data;
        Frames = frames;
    }
}

internal sealed class RenderJob
{
    public readonly NoteExpression Note;
    public readonly NoteKey Key;

    public RenderJob(NoteExpression note, NoteKey key)
    {
        Note = note;
        Key = key;
    }
}
#endregion

/// <summary>
/// Global service that pre-renders notes on background threads
/// and provides an in-memory, size-bounded LRU cache of <see cref="CachedWave"/>.
/// </summary>
public sealed class AudioPreRenderer
{
    /* ---------- public surface ---------- */

    /// <summary>Maximum memory, in bytes, that the cache may occupy (default 800 MB).</summary>
    public long MaxBytes { get; set; } = 800 * 1024 * 1024;


    private int jobCount = 0;
    /// <summary>Enqueue a note for pre-rendering (deduplicated by key).</summary>
    public void Queue(NoteExpression note)
    {
        if (note.Instrument == null || string.IsNullOrWhiteSpace(note.Instrument.Name)) throw new ArgumentException("Note must have a valid instrument with a name.", nameof(note));

        var key = new NoteKey(note);
        if (_waves.ContainsKey(key) || _inFlight.ContainsKey(key)) return;
        

        jobCount++;
        _inFlight[key] = 0;
        _jobs.Add(new RenderJob(note, key));
    }

    /// <summary>Try to retrieve a cached wave.</summary>
    public bool TryGet(NoteExpression note, out CachedWave wave)
    {
        var key = new NoteKey(note);
        if (_waves.TryGetValue(key, out wave))
        {
            TouchLru(key);
            return true;
        }
        wave = default!;
        return false;
    }

    /* ---------- cache control ---------- */

    /// <summary>Drop everything and start fresh (workers keep running).</summary>
    public void ClearCache()
    {
        lock (_lruLock)
        {
            foreach (var k in _lru)
            {
                if (_waves.TryRemove(k, out var w))
                    _bytes -= w.Bytes;
            }
            _lru.Clear();
            _waves.Clear();
            _inFlight.Clear();
            _bytes = 0;
        }
    }

    /// <summary>Stop all worker threads (they exit after finishing current job).</summary>
    public void StopWorkers() => _jobs.CompleteAdding();

    /// <summary>Restart workers if they were previously stopped.</summary>
    public void StartWorkers()
    {
        if (!_jobs.IsAddingCompleted) return;     // already running

        _jobs = new BlockingCollection<RenderJob>();
        SpawnWorkers(ComputeThreadCount());
    }

    /* ---------- implementation ---------- */

    public static readonly AudioPreRenderer Instance = new AudioPreRenderer();

    private AudioPreRenderer()
    {
        _jobs = new BlockingCollection<RenderJob>();
        SpawnWorkers(ComputeThreadCount());
    }

    /* ----- worker loop ----- */
    private void WorkerLoop()
    {
        foreach (var job in _jobs.GetConsumingEnumerable())
        {
            var wave = RenderViaMixer(job.Note);
            AddToCache(job.Key, wave);
            _inFlight.TryRemove(job.Key, out _);
        }
    }

    private CachedWave RenderViaMixer(NoteExpression note)
    {
        // Build a 1-note song
        var oneNoteSong = new Song(new ListNoteSource { note });

        // Drive a private mixer
        var mixer = new ScheduledSignalSourceMixer(prerender: false);
        mixer.ScheduleSong(oneNoteSong, null);

        const int BlockFrames = 4096;
        int channels = SoundProvider.ChannelCount;
        int blockFloats = BlockFrames * channels;

        var blockBuffer = new float[blockFloats];
        var allFloats = new List<float>();

        while (mixer.HasWork)
        {
            int read = mixer.Read(blockBuffer, 0, blockFloats);
            allFloats.AddRange(blockBuffer.AsSpan(0, read).ToArray());
        }

        // Strip leading silence that was at note.StartTime
        int sampleRate = SoundProvider.SampleRate;
        int offsetFrames = (int)Math.Round(note.StartTime.TotalSeconds * sampleRate);
        int offsetFloats = Math.Clamp(offsetFrames * channels, 0, allFloats.Count);

        int trimmedFloats = allFloats.Count - offsetFloats;
        float[] data = new float[trimmedFloats];
        allFloats.CopyTo(offsetFloats, data, 0, trimmedFloats);

        int totalFrames = trimmedFloats / channels;
        return new CachedWave(data, totalFrames);
    }

    /* ----- key / cache helpers ----- */

   

    private int cacheCount = 0;
    // LRU bookkeeping
    private void AddToCache(NoteKey key, CachedWave wave)
    {
        _waves[key] = wave;
        cacheCount++;
        SoundProvider.Debug($"Added note to cache: Cache Count {cacheCount}, Job Count: {jobCount}, Cache Size MB: {MathF.Round(_bytes / (1024 * 1024))}".ToBlue());
        lock (_lruLock)
        {
            _lru.AddFirst(key);
            _bytes += wave.Bytes;
            EvictIfNeeded();
        }
    }

    private void TouchLru(NoteKey key)
    {
        lock (_lruLock)
        {
            var node = _lru.Find(key);
            if (node == null) return;

            _lru.Remove(node);
            _lru.AddFirst(node);
        }
    }

    private void EvictIfNeeded()
    {
        while (_bytes > MaxBytes && _lru.Count > 0)
        {
            var last = _lru.Last!;
            _lru.RemoveLast();

            if (_waves.TryRemove(last.Value, out var wave))
            {
                _bytes -= wave.Bytes;
                SoundProvider.Debug($"Removed note from cache: Cache contains {_waves.Count} waves".ToMagenta());
            }
        }
    }

    /* ----- worker management ----- */
    private void SpawnWorkers(int count)
    {
        for (int i = 0; i < count; i++)
            new Thread(WorkerLoop) { IsBackground = true, Name = $"PreRender-{i}" }.Start();
    }

    private static int ComputeThreadCount()
    {
        int logical = Environment.ProcessorCount;
        return Math.Max(1, Math.Min(6, logical - 1));
    }

    /* ---------- fields ---------- */
    private ConcurrentDictionary<NoteKey, CachedWave> _waves = new();
    private ConcurrentDictionary<NoteKey, byte> _inFlight = new();
    private BlockingCollection<RenderJob> _jobs;
    private readonly LinkedList<NoteKey> _lru = new();
    private readonly object _lruLock = new();
    private long _bytes;
}
