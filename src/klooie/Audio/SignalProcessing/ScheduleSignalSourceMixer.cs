using klooie;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

public class ScheduledSongEvent : Recyclable
{
    public Song Song { get; private set; }
    // The cancellation lifetime for this song event. It was created on the app thread and should be disposed on the app thread so it only
    // get's nulled within this system, but Dispose is managed by the app thread.
    public CancellationToken? CancellationToken { get;private set; } 

    private static LazyPool<ScheduledSongEvent> pool = new LazyPool<ScheduledSongEvent>(() => new ScheduledSongEvent());
    protected ScheduledSongEvent() { }
    public static ScheduledSongEvent Create(Song song, CancellationToken? cancellationToken)
    {
        var ret = pool.Value.Rent();
        ret.Song = song;
        ret.CancellationToken = cancellationToken;
        return ret;
    }

    protected override void OnReturn()
    {
        CancellationToken = null;
        Song = null!;
        base.OnReturn();
    }
}

public class ScheduledNoteEvent : Recyclable
{
    public long StartSample; // Absolute sample offset
    public ISynthPatch Patch;
    public double DurationSeconds => Note.DurationTime.TotalSeconds;
    public NoteExpression Note { get; private set; }
    public bool IsCancelled { get; private set; }
    public NoteExpression Next;
    public NoteExpression Previous;
    public int RemainingVoices;
    private CancellationTokenRegistration? CancellationTokenRegistration;
    private void Cancel()
    {
        IsCancelled = true;
    }

    private static LazyPool<ScheduledNoteEvent> pool = new LazyPool<ScheduledNoteEvent>(() => new ScheduledNoteEvent());
    protected ScheduledNoteEvent() { }
    public static ScheduledNoteEvent Create(NoteExpression note, ISynthPatch patch, CancellationToken? cancellationToken)
    {
        var ret = pool.Value.Rent();
        ret.Note = note;
        ret.Patch = patch;
        ret.StartSample = 0;
        ret.Next = null;
        ret.Previous = null;
        ret.RemainingVoices = 0;
        ret.IsCancelled = false;

        if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
        {
            ret.Cancel();
            return ret;
        }

        ret.CancellationTokenRegistration = cancellationToken.HasValue == false ? null : cancellationToken.Value.Register(static (state) => ((ScheduledNoteEvent)state).Cancel(), ret);
        return ret;
    }

    protected override void OnReturn()
    {
        Note = null;
        if (Patch is Recyclable r) r.Dispose();
        CancellationTokenRegistration?.Dispose();
        CancellationTokenRegistration = null;
        Patch = null!;
        StartSample = 0;
        Next = null;
        Previous = null;
        RemainingVoices = 0;
        IsCancelled = false;
        base.OnReturn();
    }
}

public class ScheduledSignalSourceMixer
{
    private readonly List<ScheduledNoteEvent> scheduledNotes = new();
    private readonly ConcurrentQueue<ScheduledSongEvent> scheduledSongs = new();
    private readonly List<ActiveVoice> activeVoices = new();
    private long samplesRendered = 0;

    public bool HasWork => scheduledSongs.Count > 0 || scheduledNotes.Count > 0 || activeVoices.Count > 0;

    public long SamplesRendered => samplesRendered;

    public ScheduledSignalSourceMixer() { }

    public void ScheduleSong(Song s, CancellationToken? cancellationToken)
    {
        scheduledSongs.Enqueue(ScheduledSongEvent.Create(s, cancellationToken));
    }



    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRequested = count / SoundProvider.ChannelCount;
        long bufferStart = samplesRendered;
        long bufferEnd = bufferStart + samplesRequested;

        DrainScheduledSongs();
        RemoveCancelledNotesInQueue();
        DrainDueNotes(bufferEnd);

        Array.Clear(buffer, offset, count);
        var scratch = System.Buffers.ArrayPool<float>.Shared.Rent(count);

        MixActiveVoices(buffer, offset, samplesRequested, bufferStart, bufferEnd, scratch);

        System.Buffers.ArrayPool<float>.Shared.Return(scratch);
        samplesRendered += samplesRequested;
        return count;
    }



    private void DrainScheduledSongs()
    {
        while (scheduledSongs.TryDequeue(out var ev))
        {
            var song = ev.Song;
            var tracks = new Dictionary<string, RecyclableList<ScheduledNoteEvent>>();
            for (int i = 0; i < song.Count; i++)
            {
                var note = song[i];
                var trackKey = note.Instrument?.Name ?? "Default";
                if (tracks.TryGetValue(trackKey, out var track) == false)
                {
                    track = RecyclableListPool<ScheduledNoteEvent>.Instance.Rent(song.Count * 8);
                    tracks[trackKey] = track;
                }

                var patch = note.Instrument?.PatchFunc?.Invoke() ?? SynthLead.Create();
                if (!patch.IsNotePlayable(note.MidiNote))
                {
                    continue;
                }

                var scheduledNote = ScheduledNoteEvent.Create(note, patch, ev.CancellationToken);
                track.Items.Add(scheduledNote);
            }

            foreach (var track in tracks.Values)
            {
                for (int j = 0; j < track.Items.Count; j++)
                {
                    var noteEvent = track.Items[j];
                    noteEvent.StartSample = samplesRendered + (long)Math.Round(noteEvent.Note.StartTime.TotalSeconds * SoundProvider.SampleRate);

                    if (j > 0) noteEvent.Previous = track.Items[j - 1].Note;
                    if (j < track.Items.Count - 1) noteEvent.Next = track.Items[j + 1].Note;

                    scheduledNotes.Add(noteEvent);
                }
                track.Dispose();
            }
        }
    }

    private void RemoveCancelledNotesInQueue()
    {
         for (int i = scheduledNotes.Count - 1; i >= 0; i--)
         {
             if (scheduledNotes[i].IsCancelled)
             {
                 scheduledNotes[i].Dispose();
                 scheduledNotes.RemoveAt(i);
             }
        }
    }

    private void DrainDueNotes(long bufferEnd)
    {
        while (scheduledNotes.Count > 0 && scheduledNotes[0].StartSample < bufferEnd)
        {
            var scheduledNoteEvent = scheduledNotes[0];
            scheduledNotes.RemoveAt(0);
            if (scheduledNoteEvent.IsCancelled)
            {
                scheduledNoteEvent.Dispose();
                continue;
            }

            var voices = scheduledNoteEvent.Patch.SpawnVoices(NoteExpression.MidiNoteToFrequency(scheduledNoteEvent.Note.MidiNote), scheduledNoteEvent).ToArray();
            scheduledNoteEvent.RemainingVoices = voices.Length;

            int durSamples = (int)(scheduledNoteEvent.DurationSeconds * SoundProvider.SampleRate);
            long releaseSample = scheduledNoteEvent.StartSample + durSamples;

            for (int i = 0; i < voices.Length; i++)
            {
                activeVoices.Add(new ActiveVoice(scheduledNoteEvent, voices[i], 0, releaseSample));
            }
        }
    }

    private void MixActiveVoices(float[] buffer, int offset, int samplesRequested, long bufferStart, long bufferEnd, float[] scratch)
    {
        for (int v = activeVoices.Count - 1; v >= 0; v--)
        {
            var av = activeVoices[v];
            var noteEvent = av.NoteEvent;
            var voice = av.Voice;
            var startSample = noteEvent.StartSample;

            if (noteEvent.IsCancelled)
            {
                voice.Dispose();
                noteEvent.RemainingVoices--;
                if (noteEvent.RemainingVoices <= 0)
                {
                    noteEvent.Dispose();
                }
                activeVoices.RemoveAt(v);
                continue;
            }

            long voiceAbsoluteSample = startSample + av.SamplesPlayed;

            // If the voice starts after this buffer, skip it for now
            if (voiceAbsoluteSample >= bufferEnd)
                continue;

            int bufferWriteOffset = (int)Math.Max(0, voiceAbsoluteSample - bufferStart);

            int samplesAvailable = samplesRequested - bufferWriteOffset;
            if (samplesAvailable <= 0)
                continue;

            if (!av.Released && voiceAbsoluteSample >= av.ReleaseSample)
            {
                voice.ReleaseNote();
                av.Released = true;
            }

            int floatsNeeded = samplesAvailable * SoundProvider.ChannelCount;
            int read = voice.Render(scratch, 0, floatsNeeded);

            // === NOTEPLAYING LOGIC ===
            if (!av.Played && read > 0)
            {
                av.Played = true;
            }
            // =========================

            int bufferMixIndex = offset + bufferWriteOffset * SoundProvider.ChannelCount;
            for (int i = 0; i < read; i++)
            {
                buffer[bufferMixIndex + i] += scratch[i];
            }

            av.SamplesPlayed += read / SoundProvider.ChannelCount;

            if (voice.IsDone)
            {
                voice.Dispose();
                noteEvent.RemainingVoices--;
                if (noteEvent.RemainingVoices <= 0)
                {
                    noteEvent.Dispose();
                }
                activeVoices.RemoveAt(v);
            }
            else
            {
                activeVoices[v] = av;
            }
        }
    }

    public static async Task<RenderAnalysis> ToWav(Song song, string outputFileName, double endSilenceSeconds = 2)
    {
        var tempPath = Path.GetTempFileName();
        using var outputStream = File.OpenWrite(tempPath);
        var ret = await ToWav(song, outputStream, endSilenceSeconds, false);
        File.Move(tempPath, outputFileName, overwrite: true);
        return ret;
    }

    public static async Task<RenderAnalysis> ToWav(Song song, Stream outputStream, double endSilenceSeconds = 2, bool leaveOpen = false)
    {
        // Audio format constants
        const int sampleRate = SoundProvider.SampleRate;
        const int channels = SoundProvider.ChannelCount;
        const short bitsPerSample = 16;
        const short blockAlign = (short)(channels * (bitsPerSample / 8));
        const int bufferSamples = 4096; // Can adjust if desired
        const int bufferSize = bufferSamples * channels;
        float[] mixBuffer = new float[bufferSize];
        byte[] pcmBuffer = new byte[bufferSize * 2]; // 2 bytes per sample

        // We'll need to fill in the WAV header at the end, after we know total data length.
        long dataStartPos = outputStream.Position;
        outputStream.Position += 44; // Skip space for header

        // Main mixing loop
        long totalSamples = 0;
        bool isActive = true;
        ScheduledSignalSourceMixer mixer = new ScheduledSignalSourceMixer();
        mixer.ScheduleSong(song, null);
        // The engine needs to be scheduled with at least one song before calling ToWav!
        // Mix until all voices are finished

        var analysis = new RenderAnalysis();
        while (isActive)
        {
            int samplesRendered = mixer.Read(mixBuffer, 0, bufferSize);

            // Check if all scheduled notes and voices are done (engine must expose this, or check emptiness)
            isActive = mixer.HasWork;

            // Convert to 16-bit PCM
            for (int i = 0, o = 0; i < samplesRendered; i++)
            {
                float v = mixBuffer[i];
                // Clamp and convert to int16
                int sample = (int)MathF.Round(MathF.Max(-1f, MathF.Min(1f, v)) * 32767f);
                pcmBuffer[o++] = (byte)(sample & 0xff);
                pcmBuffer[o++] = (byte)((sample >> 8) & 0xff);
            }

            await outputStream.WriteAsync(pcmBuffer, 0, samplesRendered * 2);
            totalSamples += samplesRendered / channels;

            Array.Clear(mixBuffer, 0, samplesRendered);
            analysis.IterationCompleted(bufferSize, samplesRendered, sampleRate, channels);
        }

        // Write trailing silence if requested
        if (endSilenceSeconds > 0)
        {
            int silenceSamples = (int)(endSilenceSeconds * sampleRate) * channels;
            Array.Clear(pcmBuffer, 0, pcmBuffer.Length);
            while (silenceSamples > 0)
            {
                int chunk = Math.Min(pcmBuffer.Length / 2, silenceSamples);
                await outputStream.WriteAsync(pcmBuffer, 0, chunk * 2);
                totalSamples += chunk / channels;
                silenceSamples -= chunk;
            }
        }

        // Calculate final data size for header
        long finalPos = outputStream.Position;
        int byteRate = sampleRate * blockAlign;
        int dataLength = (int)(finalPos - dataStartPos - 44);

        // Write WAV header
        outputStream.Position = dataStartPos;
        Span<byte> header = stackalloc byte[44];

        // "RIFF" chunk descriptor
        WriteAscii(header, 0, "RIFF");
        WriteInt32(header, 4, 36 + dataLength);
        WriteAscii(header, 8, "WAVE");
        // "fmt " subchunk
        WriteAscii(header, 12, "fmt ");
        WriteInt32(header, 16, 16); // Subchunk1Size
        WriteInt16(header, 20, 1); // PCM
        WriteInt16(header, 22, (short)channels);
        WriteInt32(header, 24, sampleRate);
        WriteInt32(header, 28, byteRate);
        WriteInt16(header, 32, blockAlign);
        WriteInt16(header, 34, bitsPerSample);
        // "data" subchunk
        WriteAscii(header, 36, "data");
        WriteInt32(header, 40, dataLength);

        outputStream.Write(header);
        outputStream.Position = finalPos;
        if (!leaveOpen) outputStream.Dispose();

        // Helper methods for header
        static void WriteAscii(Span<byte> span, int pos, string s)
        {
            for (int i = 0; i < s.Length; i++)
                span[pos + i] = (byte)s[i];
        }
        static void WriteInt32(Span<byte> span, int pos, int value)
        {
            span[pos] = (byte)(value & 0xff);
            span[pos + 1] = (byte)((value >> 8) & 0xff);
            span[pos + 2] = (byte)((value >> 16) & 0xff);
            span[pos + 3] = (byte)((value >> 24) & 0xff);
        }
        static void WriteInt16(Span<byte> span, int pos, short value)
        {
            span[pos] = (byte)(value & 0xff);
            span[pos + 1] = (byte)((value >> 8) & 0xff);
        }

        return analysis;
    }

}

public struct ActiveVoice
{
    public ScheduledNoteEvent NoteEvent;
    public SynthSignalSource Voice;
    public int SamplesPlayed;
    public long ReleaseSample;
    public bool Released;
    public bool Played; // true once we've actually rendered audio for this note

    public ActiveVoice(ScheduledNoteEvent noteEvent, SynthSignalSource voice, int samplesPlayed, long releaseSample)
    {
        NoteEvent = noteEvent;
        Voice = voice;
        SamplesPlayed = samplesPlayed;
        ReleaseSample = releaseSample;
        Released = false;
        Played = false;
    }
}


public class RenderAnalysis
{
    private readonly List<BlockInfo> blocks = new();
    private long lastIterationStart;
    private readonly double ticksPerMs = Stopwatch.Frequency / 1000.0;

    public RenderAnalysis() => lastIterationStart = Stopwatch.GetTimestamp();

    // Call this at the end of each block render
    public void IterationCompleted(int requestedSamples, int renderedSamples, int sampleRate, int channels)
    {
        long now = Stopwatch.GetTimestamp();
        double ms = (now - lastIterationStart) / ticksPerMs;
        lastIterationStart = now;

        double blockDurationMs = (double)renderedSamples / channels / sampleRate * 1000.0;
        double realtimeRatio = ms / blockDurationMs;

        blocks.Add(new BlockInfo
        {
            RequestedSamples = requestedSamples,
            RenderedSamples = renderedSamples,
            DurationMs = ms,
            BlockDurationMs = blockDurationMs,
            RealtimeRatio = realtimeRatio
        });
    }

    public int TotalIterations => blocks.Count;
    public int TotalSamples => blocks.Sum(b => b.RenderedSamples);
    public double TotalRenderMs => blocks.Sum(b => b.DurationMs);
    public double TotalAudioMs => blocks.Sum(b => b.BlockDurationMs);

    public double AverageBlockMs => blocks.Count == 0 ? 0 : blocks.Average(b => b.DurationMs);
    public double SlowestBlockMs => blocks.Count == 0 ? 0 : blocks.Max(b => b.DurationMs);
    public double FastestBlockMs => blocks.Count == 0 ? 0 : blocks.Min(b => b.DurationMs);
    public double MaxRealtimeRatio => blocks.Count == 0 ? 0 : blocks.Max(b => b.RealtimeRatio);

    public int BlocksSlowerThanRealtime(double buffer = 1.0)
        => blocks.Count(b => b.RealtimeRatio > buffer);

    public bool IsRealtimeSafe(double buffer = 1.1)
        => blocks.All(b => b.RealtimeRatio <= buffer);

    public double RealtimeConfidence(double buffer = 1.1)
    {
        if (blocks.Count == 0) return 1;

        // Normalize: <1.0 = good, ==1.0 = at limit, >1.0 = slow (not realtime safe)
        var ratios = blocks.Select(b => b.RealtimeRatio / buffer).ToArray();

        // Confidence is high if almost all blocks are < 1, and only slightly reduced if a few are just above 1.
        // We'll use a smoothstep: (average of clamped 1 - ratio, 0=min, 1=max)
        double penaltySum = 0;
        foreach (var r in ratios)
        {
            double v = 1 - Math.Clamp(r, 0, 2); // 1 (perfect) .. 0 (barely at limit) .. negative for bad
                                                // For confidence, below 0 = hard fail, 0.5 = at threshold, 1 = fast.
                                                // Optionally, amplify penalty for slow blocks:
            if (v < 0) v *= 2; // Extra penalty if block was too slow
            penaltySum += Math.Clamp(v, 0, 1);
        }
        // Final score: mean of all, clamped 0–1
        return Math.Clamp(penaltySum / blocks.Count, 0, 1);
    }

    public override string ToString()
    {
        // Assume all blocks use the same channel count/sample rate as the first block.
        var ret = new StringBuilder();
        ret.AppendLine("RenderAnalysis Summary:");
        ret.AppendLine($"  Blocks:              {TotalIterations}");
        ret.AppendLine($"  Total audio frames:  {TotalFrames:N0}");
        ret.AppendLine($"  Total floats:        {TotalSamples:N0} (floats, includes all channels)");
        ret.AppendLine($"  Channels:            {SoundProvider.ChannelCount}");
        ret.AppendLine($"  Total render ms:     {TotalRenderMs:N2}");
        ret.AppendLine($"  Total audio ms:      {TotalAudioMs:N2}");
        ret.AppendLine($"  Avg block ms:        {AverageBlockMs:N2}");
        ret.AppendLine($"  Slowest block ms:    {SlowestBlockMs:N2}");
        ret.AppendLine($"  Fastest block ms:    {FastestBlockMs:N2}");
        ret.AppendLine($"  Max realtime ratio:  {MaxRealtimeRatio:N2}x");
        ret.AppendLine($"  Blocks slower than real time: {BlocksSlowerThanRealtime(1.0)}");
        ret.AppendLine($"  Is safe for realtime (10% headroom): {IsRealtimeSafe(1.1)}");
        return ret.ToString();
    }

    public int TotalFrames => blocks.Sum(b => b.RenderedSamples / SoundProvider.ChannelCount);

    private class BlockInfo
    {
        public int RequestedSamples; // floats
        public int RenderedSamples;  // floats
        public double DurationMs;
        public double BlockDurationMs;
        public double RealtimeRatio;

        public int RequestedFrames => RequestedSamples / SoundProvider.ChannelCount;
        public int RenderedFrames => RenderedSamples / SoundProvider.ChannelCount;

        public override string ToString()
        {
            return $"BlockInfo: " +
                   $"Requested={RequestedSamples} floats ({RequestedFrames} frames), " +
                   $"Rendered={RenderedSamples} floats ({RenderedFrames} frames), " +
                   $"DurationMs={DurationMs:N2}, " +
                   $"BlockDurationMs={BlockDurationMs:N2}, " +
                   $"RealtimeRatio={RealtimeRatio:N2}";
        }
    }
}