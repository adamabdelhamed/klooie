using klooie.Gaming;

namespace klooie;
public static class MelodyPlayer
{
    public static ILifetime Play(AudioPlaybackEngine engine, Melody melody)
    {
        var scheduler = Game.Current?.PausableScheduler ?? ConsoleApp.Current!.Scheduler;
        return PlayMelodyState.Play(engine, melody, scheduler);
    }

    private class PlayMelodyState : Recyclable
    {
        private PlayMelodyState() { }
        private static LazyPool<PlayMelodyState> _pool = new(() => new PlayMelodyState());
        int notesRemaining;
        public static PlayMelodyState Play(AudioPlaybackEngine engine, Melody melody, SyncronousScheduler scheduler)
        {
            var ret = _pool.Value.Rent();
            ret.notesRemaining = melody.Notes.Count;
            for (int i = 0; i < melody.Notes.Count; i++)
            {
                Note? n = melody.Notes[i];
                var noteState = PlayNoteState.Play(engine, n, scheduler);
                noteState.OnDisposed(ret, static me =>
                {
                    Interlocked.Decrement(ref me.notesRemaining);
                    if (me.notesRemaining <= 0) me.Dispose();
                });
            }
            return ret;
        }
       
    }

    private class PlayNoteState : Recyclable
    {
        private PlayNoteState() { }
        private static LazyPool<PlayNoteState> _pool = new(() => new PlayNoteState());

        private AudioPlaybackEngine engine;
        private Note note;
        private SyncronousScheduler scheduler;
        private SynthVoiceProvider voice;
        public static PlayNoteState Play(AudioPlaybackEngine engine, Note note, SyncronousScheduler scheduler)
        {
            var ret = _pool.Value.Rent();
            ret.engine = engine;
            ret.note = note;
            ret.scheduler = scheduler;
            scheduler.Delay(note.Start.TotalMilliseconds, ret, static me => me.Play());
            return ret;
        }

        private void Play()
        {
            float freq = MIDIInput.MidiNoteToFrequency(note.MidiNode);
            float volume = note.Velocity / 127f;
            var knob = VolumeKnob.Create();
            knob.Volume = volume;
            var patch = SynthPatches.CreateBass();
            voice = engine.PlaySustainedNote(freq, patch, knob);
            voice.OnDisposed(this, Recyclable.TryDisposeMe);
            scheduler.Delay(note.Duration.TotalMilliseconds, voice, static voice => voice.ReleaseNote());
        }
    }
}
