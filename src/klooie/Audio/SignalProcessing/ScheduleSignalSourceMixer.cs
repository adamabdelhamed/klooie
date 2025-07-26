using klooie;
using System.Collections.Concurrent;

public class ScheduledNoteEvent : Recyclable
{
    public long StartSample; // Absolute sample offset
    public ISynthPatch Patch;
    public double DurationSeconds => Note.DurationTime.TotalSeconds;
    public NoteExpression Note { get; private set; }
    public bool IsCancelled;

    public NoteExpression Next;
    public NoteExpression Previous;
    public int RemainingVoices;

    public void Cancel() => IsCancelled = true;

    private static LazyPool<ScheduledNoteEvent> pool = new LazyPool<ScheduledNoteEvent>(() => new ScheduledNoteEvent());
    protected ScheduledNoteEvent() { }
    public static ScheduledNoteEvent Create(NoteExpression note, ISynthPatch patch)
    {
        var ret = pool.Value.Rent();
        ret.Note = note;
        ret.Patch = patch;
        ret.StartSample = 0;
        ret.IsCancelled = false;
        ret.Next = null;
        ret.Previous = null;
        ret.RemainingVoices = 0;
        return ret;
    }

    protected override void OnReturn()
    {
        Note = null;
        if (Patch is Recyclable r) r.TryDispose();
        Patch = null!;
        StartSample = 0;
        IsCancelled = false;
        Next = null;
        Previous = null;
        RemainingVoices = 0;
        base.OnReturn();
    }
}

public class ScheduledSignalSourceMixer
{
    private readonly Queue<ScheduledNoteEvent> scheduledNotes = new();
    private readonly ConcurrentQueue<RecyclableList<ScheduledNoteEvent>> scheduledTracks = new();
    private readonly List<ActiveVoice> activeVoices = new();
    private long samplesRendered = 0;

    private Event<NoteExpression> notePlaying;
    public Event<NoteExpression> NotePlaying => notePlaying ??= Event<NoteExpression>.Create();

    public long SamplesRendered => samplesRendered;

    public ScheduledSignalSourceMixer() { }

    public void ScheduleTrack(RecyclableList<ScheduledNoteEvent> track)
    {
        scheduledTracks.Enqueue(track);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = SoundProvider.ChannelCount;
        int samplesRequested = count / channels;
        long bufferStart = samplesRendered;
        long bufferEnd = bufferStart + samplesRequested;

        while(scheduledTracks.TryDequeue(out var track))
        {
            for (int i = 0; i < track.Items.Count; i++)
            {
                var noteEvent = track.Items[i];
                noteEvent.StartSample = samplesRendered + (long)Math.Round(noteEvent.Note.StartTime.TotalSeconds * SoundProvider.SampleRate);
                
                if (i > 0)                     noteEvent.Previous = track.Items[i - 1].Note;
                if(i < track.Items.Count - 1)  noteEvent.Next = track.Items[i + 1].Note;

                scheduledNotes.Enqueue(noteEvent);
            }
            track.Dispose();
        }

        // 1. Promote any scheduled notes whose start time lands in or before this buffer
        while (scheduledNotes.TryPeek(out var note) && note.StartSample < bufferEnd)
        {
            scheduledNotes.TryDequeue(out note);
            if (note.IsCancelled)
            {
                SoundProvider.Current.EventLoop.Invoke(note, static n => n.Dispose());
                continue;
            }
            var voices = RecyclableListPool<SynthSignalSource>.Instance.Rent(8);
            try
            {
                note.Patch.SpawnVoices(
                    NoteExpression.MidiNoteToFrequency(note.Note.MidiNote),
                    SoundProvider.Current.MasterVolume,
                    note,
                    voices.Items);
                note.RemainingVoices = voices.Count;

                int durSamples = (int)(note.DurationSeconds * SoundProvider.SampleRate);
                long releaseSample = note.StartSample + durSamples;

                for (int i = 0; i < voices.Items.Count; i++)
                {
                    activeVoices.Add(new ActiveVoice(note, voices.Items[i], 0, releaseSample));
                }
            }
            finally
            {
                voices.Dispose();
            }
        }

        Array.Clear(buffer, offset, count);
        var scratch = System.Buffers.ArrayPool<float>.Shared.Rent(count);

        // 2. Mix active voices
        for (int v = activeVoices.Count - 1; v >= 0; v--)
        {
            var av = activeVoices[v];
            var noteEvent = av.NoteEvent;
            var voice = av.Voice;
            var startSample = noteEvent.StartSample;

            if (noteEvent.IsCancelled)
            {
                SoundProvider.Current.EventLoop.Invoke(voice, static (v) => v.Dispose());
                noteEvent.RemainingVoices--;
                if (noteEvent.RemainingVoices <= 0)
                    SoundProvider.Current.EventLoop.Invoke(noteEvent, static (n) => n.Dispose());
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

            int floatsNeeded = samplesAvailable * channels;
            int read = voice.Render(scratch, 0, floatsNeeded);

            // === NOTEPLAYING LOGIC ===
            if (!av.Played && read > 0)
            {
                if (notePlaying != null)
                    SoundProvider.Current.EventLoop.Invoke(() => notePlaying?.Fire(noteEvent.Note));
                av.Played = true;
            }
            // =========================

            int bufferMixIndex = offset + bufferWriteOffset * channels;
            for (int i = 0; i < read; i++)
            {
                buffer[bufferMixIndex + i] += scratch[i];
            }

            av.SamplesPlayed += read / channels;

            if (voice.IsDone)
            {
                SoundProvider.Current.EventLoop.Invoke(voice, static (v) => v.Dispose());
                noteEvent.RemainingVoices--;
                if (noteEvent.RemainingVoices <= 0)
                    SoundProvider.Current.EventLoop.Invoke(noteEvent, static (n) => n.Dispose());
                activeVoices.RemoveAt(v);
            }
            else
            {
                activeVoices[v] = av;
            }
        }

        System.Buffers.ArrayPool<float>.Shared.Return(scratch);
        samplesRendered += samplesRequested;
        return count;
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
