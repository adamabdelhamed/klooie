using System;
using System.Diagnostics;

namespace klooie;

public class MelodyComposerPlayer
{
    public Event Playing { get;private set; } = Event.Create();
    public Event Stopped { get; private set; } = Event.Create();

    public bool StopAtEnd { get; set; } = true;
    public bool IsPlaying { get; private set; }
    public double CurrentBeat { get; private set; }

    private const int AudioThreadLatency = 60;
    private Event<double> beatChanged;
    public Event<double> BeatChanged => beatChanged ??= Event<double>.Create();

    public MelodyComposer Composer { get; }

    private Recyclable? playLifetime;
    private double playheadStartBeat;
    private long? playbackStartTimestamp;

    public MelodyComposerPlayer(MelodyComposer timeline)
    {
        this.Composer = timeline ?? throw new ArgumentNullException(nameof(timeline));
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
        if(IsPlaying == false) return;
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
        var beat = playheadStartBeat + elapsedSeconds * Composer.Notes.BeatsPerMinute / 60.0;
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
        var subset = new ListNoteSource() { BeatsPerMinute = Composer.Notes.BeatsPerMinute };

        // TODO: I should not have to set the BPM for each note, but this is a quick fix
        for (int i = 0; i < Composer.Notes.Count; i++)
        {
            Composer.Notes[i].BeatsPerMinute = Composer.Notes.BeatsPerMinute;
        }

        Composer.Notes.SortMelody();
        for (int i = 0; i < Composer.Notes.Count; i++)
        {
            NoteExpression? n = Composer.Notes[i];
            double endBeat = n.DurationBeats >= 0 ? n.StartBeat + n.DurationBeats : double.PositiveInfinity;
            if (endBeat <= startBeat) continue;

            double relStart = n.StartBeat - startBeat;
            if (relStart < 0) relStart = 0;

            double duration = n.DurationBeats;
            if (n.DurationBeats >= 0 && n.StartBeat < startBeat)
            {
                duration = endBeat - startBeat;
            }

            subset.Add(NoteExpression.Create(n.MidiNote, relStart, duration, Composer.Notes.BeatsPerMinute, n.Velocity, n.Instrument));
        }

        if (subset.Count == 0) return;

        ConsoleApp.Current.Sound.Play(new Song(subset, Composer.Notes.BeatsPerMinute), playLifetime);
    }
}
