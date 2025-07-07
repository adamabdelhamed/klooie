namespace klooie;
public static class MelodyPlayer
{
    public static void Play(this ISoundProvider engine, Melody melody)
    {
        long scheduleZero = engine.SamplesRendered;
        foreach (var note in melody.Notes)
        {
            float freq = MIDIInput.MidiNoteToFrequency(note.MidiNode);
            float volume = note.Velocity / 127f;
            var patch = SynthPatches.CreateBass();
            long startSample = scheduleZero + (long)Math.Round(note.Start.TotalSeconds * AudioPlaybackEngine.SampleRate);
            engine.ScheduleSynthNote(note.MidiNode, startSample, note.Duration.TotalSeconds, note.Velocity, patch);
        }
    }
}
