namespace klooie;
public static class MelodyPlayer
{
    public static ILifetime Play(this AudioPlaybackEngine engine, Melody melody) => PlayMelodyState.Play(engine, melody);
    private class PlayMelodyState : Recyclable
    {
        private PlayMelodyState() { }
        private static LazyPool<PlayMelodyState> _pool = new(() => new PlayMelodyState());
        int notesRemaining;
        public static PlayMelodyState Play(AudioPlaybackEngine engine, Melody melody)
        {
            var ret = _pool.Value.Rent();
            ret.notesRemaining = melody.Notes.Count;
            long scheduleZero = engine.scheduledSynthProvider.SamplesRendered;
            foreach (var note in melody.Notes)
            {
                float freq = MIDIInput.MidiNoteToFrequency(note.MidiNode);
                float volume = note.Velocity / 127f;
                var knob = VolumeKnob.Create();
                knob.Volume = volume;
                var patch = SynthPatches.CreateBass();
                long startSample = scheduleZero + (long)Math.Round(note.Start.TotalSeconds * AudioPlaybackEngine.SampleRate);

                engine.ScheduleSynthNote(note.MidiNode, startSample, note.Duration.TotalSeconds, note.Velocity, patch);
            }
            return ret;
        }
    }

}
