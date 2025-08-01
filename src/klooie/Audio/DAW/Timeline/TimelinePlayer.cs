using System;
using System.Diagnostics;

namespace klooie;

/// <summary>
/// Centralized controller that manages playback for a timeline.
/// It exposes play, stop and seeking functionality while
/// raising an event whenever the playhead position changes.
/// </summary>
public class TimelinePlayer
{
    public Event Playing { get;private set; } = Event.Create();
    public Event Stopped { get; private set; } = Event.Create();
    private readonly Func<double> maxBeatProvider;
    private double playheadStartBeat;
    private long? playbackStartTimestamp;
    public bool StopAtEnd { get; set; } = true;
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Beats per minute used to convert time to beats.
    /// </summary>
    public double BeatsPerMinute { get; set; }

    /// <summary>
    /// Current playhead position in beats.
    /// </summary>
    public double CurrentBeat { get; private set; }

    private Event<double> beatChanged;
    /// <summary>
    /// Event fired whenever <see cref="CurrentBeat"/> changes.
    /// </summary>
    public Event<double> BeatChanged => beatChanged ??= Event<double>.Create();

    public VirtualTimelineGrid Timeline { get; }

    public TimelinePlayer(VirtualTimelineGrid timeline, Func<double> maxBeatProvider, double bpm)
    {
        this.Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        this.maxBeatProvider = maxBeatProvider;
        this.BeatsPerMinute = bpm;
        BeatChanged.Subscribe(this, static (me, b) => me.Timeline.Viewport.OnBeatChanged(b), Timeline);
    }
    private Recyclable? playLifetime;
    public void Play()
    {
        if (IsPlaying) return;

        ConsoleApp.Current.Scheduler.Delay(60, StartDelayed);
        playLifetime?.TryDispose();
        playLifetime = DefaultRecyclablePool.Instance.Rent();
        PlayAudio(CurrentBeat, playLifetime);
    }

    private void StartDelayed()
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
        var beat = playheadStartBeat + elapsedSeconds * BeatsPerMinute / 60.0;
        var maxBeat = maxBeatProvider();
        if (StopAtEnd && beat > maxBeat + 4)
        {
            CurrentBeat = maxBeat;
            Stop();
        }
        else
        {
            CurrentBeat = beat;
        }
    }

    private void PlayAudio(double startBeat, ILifetime? playLifetime = null)
    {
        var subset = new ListNoteSource() { BeatsPerMinute = Timeline.Notes.BeatsPerMinute };

        // TODO: I should not have to set the BPM for each note, but this is a quick fix
        for (int i = 0; i < Timeline.Notes.Count; i++)
        {
            Timeline.Notes[i].BeatsPerMinute = Timeline.Notes.BeatsPerMinute;
        }

        Timeline.Notes.SortMelody();
        for (int i = 0; i < Timeline.Notes.Count; i++)
        {
            NoteExpression? n = Timeline.Notes[i];
            double endBeat = n.DurationBeats >= 0 ? n.StartBeat + n.DurationBeats : double.PositiveInfinity;
            if (endBeat <= startBeat) continue;

            double relStart = n.StartBeat - startBeat;
            if (relStart < 0) relStart = 0;

            double duration = n.DurationBeats;
            if (n.DurationBeats >= 0 && n.StartBeat < startBeat)
            {
                duration = endBeat - startBeat;
            }

            subset.Add(NoteExpression.Create(n.MidiNote, relStart, duration, Timeline.Notes.BeatsPerMinute, n.Velocity, n.Instrument));
        }

        if (subset.Count == 0) return;

        ConsoleApp.Current.Sound.Play(new Song(subset, Timeline.Notes.BeatsPerMinute), playLifetime);
    }
}
