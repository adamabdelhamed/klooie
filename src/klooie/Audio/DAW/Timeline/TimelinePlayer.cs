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

    public TimelinePlayer(Func<double> maxBeatProvider, double bpm)
    {
        this.maxBeatProvider = maxBeatProvider;
        this.BeatsPerMinute = bpm;
    }

    public void Start(double? startBeat = null)
    {
        if (IsPlaying) return;
        Playing.Fire();
        playheadStartBeat = startBeat ?? CurrentBeat;
        CurrentBeat = playheadStartBeat;
        playbackStartTimestamp = Stopwatch.GetTimestamp();
        IsPlaying = true;
        ScheduleTick();
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
        IsPlaying = false;
        playbackStartTimestamp = null;
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
        if (StopAtEnd && beat > maxBeat)
        {
            CurrentBeat = maxBeat;
            Stop();
        }
        else
        {
            CurrentBeat = beat;
        }
    }
}
