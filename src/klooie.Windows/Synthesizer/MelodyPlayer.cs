namespace klooie;
public static class MelodyPlayer
{
    public static ILifetime Play(this AudioPlaybackEngine engine, Melody melody) => PlayMelodyState.Play(engine, melody);
    private class PlayMelodyState : Recyclable
    {
        private static SyncronousScheduler? scheduler;

        private static SyncronousScheduler Scheduler 
        {
             get
            {
                if(scheduler != null) return scheduler;
                if(ConsoleApp.Current == null) throw new InvalidOperationException("MelodyPlayer requires a ConsoleApp to be running. Please start a ConsoleApp before using MelodyPlayer.");
                scheduler = new SyncronousScheduler(ConsoleApp.Current);
                // Use end of cycle instead of AfterPaint since AfterPaint only fires every 33ms or so. 
                // We want our music to play on time and EndOfCycle is called every time the app loop runs.
                // The app burns the CPU and never sleeps or yields so the scheduler will run as fast as the CPU can handle
                // making it very unlikely that the music will be late.
                scheduler.Mode = SyncronousScheduler.ExecutionMode.EndOfCycle;
                // Ensure the scheduler is disposed when the app is disposed. Since we're single threaded the use of a static scheduler is fine.
                ConsoleApp.Current.OnDisposed(static () => scheduler = null);
                return scheduler;
            }
        }

        private PlayMelodyState() { }
        private static LazyPool<PlayMelodyState> _pool = new(() => new PlayMelodyState());
        int notesRemaining;
        public static PlayMelodyState Play(AudioPlaybackEngine engine, Melody melody)
        {
            var ret = _pool.Value.Rent();
            ret.notesRemaining = melody.Notes.Count;
            for (int i = 0; i < melody.Notes.Count; i++)
            {
                Note? n = melody.Notes[i];
                var noteState = PlayNoteState.Play(engine, n, Scheduler);
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
