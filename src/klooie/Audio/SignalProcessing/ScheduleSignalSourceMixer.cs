using klooie;
using System.Collections.Concurrent;

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

    private Event<NoteExpression> notePlaying;
    public Event<NoteExpression> NotePlaying => notePlaying ??= Event<NoteExpression>.Create();

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
                if (notePlaying != null)
                    SoundProvider.Current.EventLoop.Invoke(() => notePlaying?.Fire(noteEvent.Note));
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
