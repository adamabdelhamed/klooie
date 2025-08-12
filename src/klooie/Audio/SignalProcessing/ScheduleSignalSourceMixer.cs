using klooie;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    public double DurationSeconds => Note.DurationTime.TotalSeconds;
    public NoteExpression Note { get; private set; }
    public bool IsCancelled { get; private set; }
    public NoteExpression Next;
    public NoteExpression Previous;
    public int RemainingVoices;
    private CancellationTokenRegistration? CancellationTokenRegistration;

    public ISynthPatch? Patch;
    private void Cancel()
    {
        IsCancelled = true;
    }

    private static LazyPool<ScheduledNoteEvent> pool = new LazyPool<ScheduledNoteEvent>(() => new ScheduledNoteEvent());
    protected ScheduledNoteEvent() { }
    public static ScheduledNoteEvent Create(NoteExpression note, CancellationToken? cancellationToken)
    {
        var ret = pool.Value.Rent();
        ret.Note = note;
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
        CancellationTokenRegistration?.Dispose();
        CancellationTokenRegistration = null;
        StartSample = 0;
        Next = null;
        Previous = null;
        RemainingVoices = 0;
        IsCancelled = false;
        if(Patch != null && Patch is Recyclable recyclablePatch)
        {
            recyclablePatch.Dispose();
            Patch = null;
        }
        base.OnReturn();
    }
}

public enum ScheduledSignalMixerMode
{
    Realtime,
    RealtimeWithPreRenderOptimized,
    PreRenderOnly
}

public class ScheduledSignalSourceMixer
{
    private readonly List<ScheduledNoteEvent> scheduledNotes = new();
    private readonly ConcurrentQueue<ScheduledSongEvent> scheduledSongs = new();
    private readonly List<ActiveVoice> activeVoices = new();
    private long samplesRendered = 0;
    private readonly List<ActiveVoiceBuffered> bufferedVoices = new();

    public ScheduledSignalMixerMode Mode { get; set; }
    public bool HasWork => scheduledSongs.Count > 0 || scheduledNotes.Count > 0 || activeVoices.Count > 0 || bufferedVoices.Count > 0;

    public long SamplesRendered => samplesRendered;
    public ScheduledSignalSourceMixer(ScheduledSignalMixerMode mode = ScheduledSignalMixerMode.RealtimeWithPreRenderOptimized) 
    {
        this.Mode = mode;
    }

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
        var scratch = RecyclableListPool<float>.Instance.Rent(count);
        for(var i = 0; i < count; i++) scratch.Items.Add(0f);
    
        MixActiveVoices(buffer, offset, samplesRequested, bufferStart, bufferEnd, scratch.Items);

        scratch.Dispose();
        samplesRendered += samplesRequested;
        return count;
    }



    private void DrainScheduledSongs()
    {
        var didWork = false; 
        while (scheduledSongs.TryDequeue(out var ev))
        {
            didWork = true;
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


                var scheduledNote = ScheduledNoteEvent.Create(note, ev.CancellationToken);
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
                    if (Mode == ScheduledSignalMixerMode.RealtimeWithPreRenderOptimized || Mode == ScheduledSignalMixerMode.PreRenderOnly)
                    {
                        AudioPreRenderer.Instance.Queue(noteEvent.Note);
                    }
                }
                track.Dispose();
            }
        }

        if(didWork)
        {
            scheduledNotes.Sort(new Comparison<ScheduledNoteEvent>((a,b) => a.StartSample.CompareTo(b.StartSample)));
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
        // mode might be changed by another thread so grab it here and use
        // it for the entire loop. Changing mode is supported, but not mid
        // loop.
        var mode = Mode;
        while (scheduledNotes.Count > 0 && scheduledNotes[0].StartSample < bufferEnd)
        {
            var scheduledNoteEvent = scheduledNotes[0];
            CachedWave? preRenderedWave = null;

            // If we're in pre-render only mode, then we wait to schedule this note until it has been pre-rendered.
            if (mode == ScheduledSignalMixerMode.PreRenderOnly && AudioPreRenderer.Instance.TryGet(scheduledNoteEvent.Note, out preRenderedWave) == false) break;
            

            scheduledNotes.RemoveAt(0);
            if (scheduledNoteEvent.IsCancelled)
            {
                scheduledNoteEvent.Dispose();
                continue;
            }

            // If we are in either pre-render mode, try to get a pre-rendered wave for this note. It will likely miss if we are in pre-render only mode since we just checked above,
            // but in the case of RealtimeWithPreRenderOptimized, we can still use a cached wave if it exists.
            preRenderedWave = preRenderedWave != null ? preRenderedWave : mode == ScheduledSignalMixerMode.RealtimeWithPreRenderOptimized && AudioPreRenderer.Instance.TryGet(scheduledNoteEvent.Note, out preRenderedWave) ? preRenderedWave : null;

            // If we have a pre-rendered wave, we can use it directly without synthesizing any sound.
            if (preRenderedWave != null)
            {
                bufferedVoices.Add(new ActiveVoiceBuffered { NoteEvent = scheduledNoteEvent, Wave = preRenderedWave, FrameCursor = 0  });
                SoundProvider.Debug($"Scheduled note {scheduledNoteEvent.Note.MidiNote} from {scheduledNoteEvent.Note.Instrument?.Name ?? "Patch"} with cached wave.".ToDarkGreen());
                continue;          
            }

            SoundProvider.Debug($"Scheduled note {scheduledNoteEvent.Note.MidiNote} from {scheduledNoteEvent.Note.Instrument?.Name ?? "Patch"} with live spawning.".ToOrange());
            var patch = scheduledNoteEvent.Note.Instrument?.PatchFunc(scheduledNoteEvent.Note) ?? SynthLead.Create(scheduledNoteEvent.Note);
            if (!patch.IsNotePlayable(scheduledNoteEvent.Note.MidiNote))
            {
                if (patch is Recyclable r) r.Dispose();
                continue;
            }
            scheduledNoteEvent.Patch = patch;
            var voices = patch.SpawnVoices(NoteExpression.MidiNoteToFrequency(scheduledNoteEvent.Note.MidiNote), scheduledNoteEvent).ToArray();
 
            scheduledNoteEvent.RemainingVoices = voices.Length;
            int durSamples = (int)Math.Round(scheduledNoteEvent.DurationSeconds * SoundProvider.SampleRate);
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                var delay = voice.Envelope.Delay;
                int delaySamples = (int)Math.Round(delay * SoundProvider.SampleRate);
                long startSample = scheduledNoteEvent.StartSample + delaySamples;
                long releaseSample = startSample + durSamples; // push by delay so the voice gets full playtime
                // pass the per-voice delay so the synth starts this voice later
                activeVoices.Add(new ActiveVoice(scheduledNoteEvent, voices[i], delaySamples, releaseSample));
            }
        }
    }

    private void MixActiveVoices(float[] buffer, int offset, int samplesRequested, long bufferStart, long bufferEnd, List<float> scratch)
    {
        MixBufferedVoices(buffer, offset, samplesRequested, bufferStart);
        MixRealTimeVoices(buffer, offset, samplesRequested, bufferStart, bufferEnd, scratch);
    }

    private void MixBufferedVoices(float[] buffer, int offset, int samplesRequested, long bufferStart)
    {
        for (int b = bufferedVoices.Count - 1; b >= 0; b--)
        {
            var bv = bufferedVoices[b];
            if (bv.NoteEvent.IsCancelled)
            {
                bv.NoteEvent.Dispose();
                bufferedVoices.RemoveAt(b);
                continue;
            }

            long absStart = bv.NoteEvent.StartSample;
            int frameCursor = bv.FrameCursor;
            long relPosFrames = absStart + frameCursor - bufferStart;
            int srcFrame = frameCursor;
            int dst;

            int framesAvailable = bv.Wave.Frames - srcFrame;
            int framesThisPass;

            if (relPosFrames < 0)
            {
                // Note started before this buffer: skip some frames in the cached wave
                int framesToSkip = (int)(-relPosFrames);
                srcFrame += framesToSkip;
                if (srcFrame >= bv.Wave.Frames) { bv.NoteEvent.Dispose(); bufferedVoices.RemoveAt(b); continue; }
                dst = offset;
                framesThisPass = Math.Min(framesAvailable - framesToSkip, samplesRequested);
            }
            else
            {
                // Note starts at or after this buffer
                dst = offset + (int)relPosFrames * SoundProvider.ChannelCount;
                framesThisPass = Math.Min(framesAvailable, samplesRequested - (int)relPosFrames);
            }
            if (framesThisPass <= 0) continue;

            int src = srcFrame * SoundProvider.ChannelCount;
            int floats = framesThisPass * SoundProvider.ChannelCount;

            // SAFETY: Do not mix past end of output buffer
            if (dst + floats > buffer.Length) floats = buffer.Length - dst;
            framesThisPass = Math.Min(framesThisPass, floats / SoundProvider.ChannelCount);

            for (int i = 0; i < floats; i++)
                buffer[dst + i] += bv.Wave.Data[src + i];

            bv.FrameCursor = srcFrame + framesThisPass;

            if (bv.FrameCursor >= bv.Wave.Frames)
            {
                bv.NoteEvent.Dispose();
                bufferedVoices.RemoveAt(b);
            }
        }
    }

    private void MixRealTimeVoices(float[] buffer, int offset, int samplesRequested, long bufferStart, long bufferEnd, List<float> scratch)
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
            Span<float> span = CollectionsMarshal.AsSpan(scratch);
            int read = voice.Render(span, 0, floatsNeeded);

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
        const int bufferSamples = 4096; // tweaking this can affect the sound itself. It probably should not be able to, but it can so don't tweak this.  
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

struct ActiveVoiceBuffered
{
    public ScheduledNoteEvent NoteEvent;
    public CachedWave Wave;
    public int FrameCursor;         // how many frames already mixed
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

    public double RealtimeConfidence(double buffer = 1.1, double comfortable = 0.55)
    {
        if (blocks.Count == 0) return 1;     // trivial case

        // Grab all ratios once. Sorting is O(n log n) but still trivial vs. audio work.
        var ratios = blocks.Select(b => b.RealtimeRatio).ToArray();
        Array.Sort(ratios);

        double worst = ratios[^1];
        double p95 = ratios[(int)(ratios.Length * 0.95)];
        int overruns = ratios.Count(r => r > buffer);
        double overrunFrac = overruns / (double)ratios.Length;    // 0 – 1

        // 1) Every block is well below the comfortable threshold → perfect score
        if (worst <= comfortable)
            return 1.0;

        // 2) No block exceeds the buffer → 0.95 – 1.00 range
        if (worst <= buffer)
        {
            // Linear interpolation: 0 at 'buffer', 1 at 'comfortable'
            double headroom = (buffer - worst) / (buffer - comfortable);   // 0 – 1
                                                                           // Feather it with the spike-free quality (how many near misses?)
            double stability = 1.0 - overrunFrac;                          // 1 if none near limit
            return 0.95 + 0.05 * headroom * stability;
        }

        // 3) We have overruns → < 0.95.  Combine how bad & how often.
        double headroomFactor = buffer / worst;        // < 1.  e.g. worst=1.5 => 0.733
        double stabilityFactor = 1.0 - overrunFrac;    // 0 – 1
        double score = 0.95 * headroomFactor * stabilityFactor;

        // Guard against pathological negatives if something crazy happens
        return Math.Clamp(score, 0, 0.949);
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