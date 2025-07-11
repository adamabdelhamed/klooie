using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class ScheduledNoteEvent : Recyclable
{
    public long StartSample; // Absolute sample offset
    public SynthSignalSource Voice;
    public double DurationSeconds => Note.DurationTime.TotalSeconds;
    public NoteExpression Note { get; private set; }

    private static LazyPool<ScheduledNoteEvent> pool = new LazyPool<ScheduledNoteEvent>(() => new ScheduledNoteEvent());
    protected ScheduledNoteEvent() { }
    public static ScheduledNoteEvent Create(long startSample, NoteExpression note, SynthSignalSource voice)
    {
        var ret = pool.Value.Rent();
        ret.Note = note;
        ret.StartSample = startSample;
        ret.Voice = voice;
        return ret;
    }

    protected override void OnReturn()
    {
        Note = null;
        base.OnReturn();
    }
}

public class ScheduledSignalSourceMixer
{
    private readonly ConcurrentQueue<ScheduledNoteEvent> scheduledNotes = new();
    private readonly List<(SynthSignalSource Voice, long StartSample, int SamplesPlayed, long ReleaseSample, bool Released)> activeVoices = new();
    private long samplesRendered = 0;

    private Event<ScheduledNoteEvent> notePlaying;
    public Event<ScheduledNoteEvent> NotePlaying => notePlaying ??= Event<ScheduledNoteEvent>.Create();

    public long SamplesRendered => samplesRendered;
    public ScheduledSignalSourceMixer()
    {

    }

    public void ScheduleNote(ScheduledNoteEvent note) => scheduledNotes.Enqueue(note);

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = SoundProvider.ChannelCount;
        int samplesRequested = count / channels;
        long bufferStart = samplesRendered;
        long bufferEnd = bufferStart + samplesRequested;

        // 1. Promote any scheduled notes whose start time lands in or before this buffer
        while (scheduledNotes.TryPeek(out var note) && note.StartSample < bufferEnd)
        {
            scheduledNotes.TryDequeue(out note);
            notePlaying?.Fire(note);
            var voice = note.Voice;
            int durSamples = (int)(note.DurationSeconds * SoundProvider.SampleRate);
            long releaseSample = note.StartSample + durSamples;
            activeVoices.Add((voice, note.StartSample, 0, releaseSample, false));
            note.Dispose();
        }

        Array.Clear(buffer, offset, count);
        var scratch = System.Buffers.ArrayPool<float>.Shared.Rent(count);

        // 2. Mix active voices
        for (int v = activeVoices.Count - 1; v >= 0; v--)
        {
            var (voice, startSample, samplesPlayed, releaseSample, released) = activeVoices[v];

            // Calculate where the voice's next sample lands in this buffer
            long voiceAbsoluteSample = startSample + samplesPlayed;

            // If the voice starts after this buffer, skip it for now
            if (voiceAbsoluteSample >= bufferEnd)
                continue;

            // Determine where to start mixing in the output buffer
            int bufferWriteOffset = (int)Math.Max(0, voiceAbsoluteSample - bufferStart);
            // Determine how many samples from the voice to skip (if the buffer starts before the voice)
            int voiceReadOffset = (int)Math.Max(0, bufferStart - startSample);

            // The max samples we can mix from this voice into this buffer
            int samplesAvailable = samplesRequested - bufferWriteOffset;
            if (samplesAvailable <= 0)
                continue;

            // Release note if needed
            if (!released && voiceAbsoluteSample >= releaseSample)
            {
                voice.ReleaseNote();
                released = true;
            }

            // Read from the voice: always start from where the voice itself left off
            int floatsNeeded = samplesAvailable * channels;
            int read = voice.Render(scratch, 0, floatsNeeded);

            // Mix into the output buffer at the correct offset
            int bufferMixIndex = offset + bufferWriteOffset * channels;
            for (int i = 0; i < read; i++)
            {
                buffer[bufferMixIndex + i] += scratch[i];
            }

            samplesPlayed += read / channels;

            // Check if voice is done
            bool done = voice.IsDone;
            if (done)
            {
                voice.Dispose();
                activeVoices.RemoveAt(v);
            }
            else
            {
                activeVoices[v] = (voice, startSample, samplesPlayed, releaseSample, released);
            }
        }

        System.Buffers.ArrayPool<float>.Shared.Return(scratch);
        samplesRendered += samplesRequested;
        return count;
    }
}
