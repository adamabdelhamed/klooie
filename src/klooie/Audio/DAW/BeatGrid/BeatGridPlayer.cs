using System;
using System.Diagnostics;

namespace klooie;

public class BeatGridPlayer<T>
{
    public Event Playing { get; private set; } = Event.Create();
    public Event Stopped { get; private set; } = Event.Create();

    public bool StopAtEnd { get; set; } = true;
    public bool IsPlaying { get; private set; }
    public double CurrentBeat { get; private set; }

    private const int AudioThreadLatency = 60;
    private Event<double> beatChanged;
    public Event<double> BeatChanged => beatChanged ??= Event<double>.Create();

    public BeatGrid<T> Grid { get; }

    private Recyclable? playLifetime;
    private double playheadStartBeat;
    private long? playbackStartTimestamp;

    public BeatGridPlayer(BeatGrid<T> grid)
    {
        this.Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        BeatChanged.Subscribe(this, static (me, b) => me.Grid.Viewport.OnBeatChanged(b), Grid);
    }

    public void Play()
    {
        if (IsPlaying) return;
        if(CurrentBeat >= Grid.MaxBeat && StopAtEnd)
        {
            CurrentBeat = 0; // Reset to start if at or past the end
        }
        var autoStopSuffix = StopAtEnd ? " (auto-stop)" : "";
        Grid.StatusChanged.Fire(ConsoleString.Parse($"[White]Playing... {autoStopSuffix}"));

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
        var beat = playheadStartBeat + elapsedSeconds * Grid.BeatsPerMinute / 60.0;

        if (StopAtEnd && beat > Grid.MaxBeat)
        {
            CurrentBeat = Grid.MaxBeat;
            Stop();
        }
        else
        {
            CurrentBeat = beat;
        }
    }

    private void PlayAudio(double startBeat, ILifetime? playLifetime = null)
    {
        var song = Grid.Compose();
        var subset = new ListNoteSource() { BeatsPerMinute = song.Notes.BeatsPerMinute };

        // TODO: I should not have to set the BPM for each note, but this is a quick fix
        for (int i = 0; i < song.Notes.Count; i++)
        {
            song.Notes[i].BeatsPerMinute = song.Notes.BeatsPerMinute;
        }

        song.Notes.SortMelody();
        for (int i = 0; i < song.Notes.Count; i++)
        {
            NoteExpression? n = song.Notes[i];
            double endBeat = n.DurationBeats >= 0 ? n.StartBeat + n.DurationBeats : double.PositiveInfinity;
            if (endBeat <= startBeat) continue;

            double relStart = n.StartBeat - startBeat;
            if (relStart < 0) relStart = 0;

            double duration = n.DurationBeats;
            if (n.DurationBeats >= 0 && n.StartBeat < startBeat)
            {
                duration = endBeat - startBeat;
            }

            subset.Add(NoteExpression.Create(n.MidiNote, relStart, duration, song.Notes.BeatsPerMinute, n.Velocity, n.Instrument));
        }

        if (subset.Count == 0) return;

        ConsoleApp.Current.Sound.Play(new Song(subset, song.Notes.BeatsPerMinute), playLifetime);
    }
}
