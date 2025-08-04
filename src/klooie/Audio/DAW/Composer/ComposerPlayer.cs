using System;
using System.Diagnostics;

namespace klooie;

public class ComposerPlayer
{
    public Event Playing { get; private set; } = Event.Create();
    public Event Stopped { get; private set; } = Event.Create();

    public bool StopAtEnd { get; set; } = true;
    public bool IsPlaying { get; private set; }
    public double CurrentBeat { get; private set; }

    private const int AudioThreadLatency = 60;
    private Event<double> beatChanged;
    public Event<double> BeatChanged => beatChanged ??= Event<double>.Create();

    public Composer Composer { get; }

    private Recyclable? playLifetime;
    private double playheadStartBeat;
    private long? playbackStartTimestamp;

    public ComposerPlayer(Composer composer)
    {
        this.Composer = composer ?? throw new ArgumentNullException(nameof(composer));
        BeatChanged.Subscribe(this, static (me, b) => me.Composer.Viewport.OnBeatChanged(b), Composer);
    }

    public void Play()
    {
        if (IsPlaying) return;

        var autoStopSuffix = StopAtEnd ? " (auto-stop)" : "";
        Composer.StatusChanged.Fire(ConsoleString.Parse($"[White]Playing... {autoStopSuffix}"));

        ConsoleApp.Current.Scheduler.Delay(AudioThreadLatency, StartMovingPlayHeadAfterAudioThreadLatency);
        playLifetime?.TryDispose();
        playLifetime = DefaultRecyclablePool.Instance.Rent();
        PlayAudio(CurrentBeat, playLifetime);
    }

    private void StartMovingPlayHeadAfterAudioThreadLatency()
    {
        Playing.Fire();
        playheadStartBeat = CurrentBeat;
        playbackStartTimestamp = Stopwatch.GetTimestamp();
        IsPlaying = true;
        ScheduleTick();
    }

    public void Pause()
    {
        if (!IsPlaying) return;
        playLifetime?.TryDispose();
        playLifetime = null;
        UpdateCurrentBeat();
        IsPlaying = false;
        beatChanged?.Fire(CurrentBeat);
    }

    public void Resume()
    {
        if (IsPlaying) return;
        Playing.Fire();
        playheadStartBeat = CurrentBeat;
        playbackStartTimestamp = Stopwatch.GetTimestamp();
        IsPlaying = true;
        ScheduleTick();
        beatChanged?.Fire(CurrentBeat);
    }

    public void Stop()
    {
        if (!IsPlaying) return;
        Stopped.Fire();
        playbackStartTimestamp = null;
        IsPlaying = false;
        beatChanged?.Fire(CurrentBeat);
    }

    public void Seek(double beat)
    {
        CurrentBeat = Math.Max(0, beat);
        playheadStartBeat = CurrentBeat;
        playbackStartTimestamp = IsPlaying ? Stopwatch.GetTimestamp() : playbackStartTimestamp;
        beatChanged?.Fire(CurrentBeat);
    }

    public void SeekBy(double deltaBeats) => Seek(CurrentBeat + deltaBeats);

    private void ScheduleTick()
    {
        if (!IsPlaying) return;
        ConsoleApp.Current.Scheduler.Delay(10, Tick);
    }

    private void Tick()
    {
        if (!IsPlaying) return;
        UpdateCurrentBeat();
        beatChanged?.Fire(CurrentBeat);
        ScheduleTick();
    }

    private void UpdateCurrentBeat()
    {
        if (playbackStartTimestamp == null) return;
        double elapsedSeconds = Stopwatch.GetElapsedTime(playbackStartTimestamp.Value).TotalSeconds;

        // Use BPM of the first clip with any notes, or default to 120
        double bpm = 120.0;
        foreach (var track in Composer.Tracks)
        {
            if (track.Melodies.Count > 0 && track.Melodies[0].Melody != null)
            {
                bpm = track.Melodies[0].Melody.BeatsPerMinute;
                break;
            }
        }

        var beat = playheadStartBeat + elapsedSeconds * bpm / 60.0;

        if (StopAtEnd && beat > Composer.MaxBeat + 4)
        {
            CurrentBeat = Composer.MaxBeat;
            Stop();
        }
        else
        {
            CurrentBeat = beat;
        }
    }

    private void PlayAudio(double startBeat, ILifetime? playLifetime = null)
    {
        // "Flatten" all visible melody clips into a single note sequence, adjusting start times for each clip.
        var subset = new ListNoteSource();

        // Find global BPM (first non-null Melody BPM, or 120)
        double bpm = 120.0;
        foreach (var track in Composer.Tracks)
        {
            var firstClip = track.Melodies.FirstOrDefault();
            if (firstClip?.Melody != null)
            {
                bpm = firstClip.Melody.BeatsPerMinute;
                break;
            }
        }
        subset.BeatsPerMinute = bpm;

        foreach (var track in Composer.Tracks)
        {
            foreach (var clip in track.Melodies)
            {
                if (clip.Melody == null) continue;

                // Only include notes in the clip that overlap with the current playback region
                foreach (var note in clip.Melody)
                {
                    double globalStart = clip.StartBeat + note.StartBeat;
                    double endBeat = globalStart + note.DurationBeats;
                    if (endBeat <= startBeat) continue; // Skip notes that end before the playhead

                    double relStart = globalStart - startBeat;
                    if (relStart < 0) relStart = 0;

                    double duration = note.DurationBeats;
                    if (note.DurationBeats >= 0 && globalStart < startBeat)
                        duration = endBeat - startBeat;

                    // Set correct BPM on the note (quick fix)
                    var nn = NoteExpression.Create(note.MidiNote, relStart, duration, bpm, note.Velocity, note.Instrument);
                    subset.Add(nn);
                }
            }
        }

        if (subset.Count == 0) return;

        // Play as a single song (mixing all notes together)
        ConsoleApp.Current.Sound.Play(new Song(subset, bpm), playLifetime);
    }
}
