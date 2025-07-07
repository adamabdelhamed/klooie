namespace klooie;
public static class MelodyPlayer
{
    private static Comparison<Note> MelodyNoteComparer = (a, b) => a.Start.CompareTo(b.Start);

    public static void Play(this ISoundProvider engine, Melody melody)
    {
        const double bufferDelaySeconds = 0.1; // 100ms buffer for safety
        long scheduleZero = engine.SamplesRendered + (long)(bufferDelaySeconds * SoundProvider.SampleRate);
        melody.Notes.Sort(MelodyNoteComparer);
        for (int i = 0; i < melody.Notes.Count; i++)
        {
            Note? note = melody.Notes[i];
            long startSample = scheduleZero + (long)Math.Round(note.Start.TotalSeconds * SoundProvider.SampleRate);
            engine.ScheduleSynthNote(note.MidiNode, startSample, note.Duration.TotalSeconds, note.Velocity, note.Patch ?? SynthPatches.CreateBass());
        }
    }
}
