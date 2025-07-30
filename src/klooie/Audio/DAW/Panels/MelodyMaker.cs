using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MelodyMaker : ProtectedConsolePanel
{
    private PianoWithTimeline pianoWithTimeline;
    private ListNoteSource noteSource;
    private Dictionary<int, SustainedNoteTracker> noteTrackers = new Dictionary<int, SustainedNoteTracker>();
    private ConsoleApp app;
    private IMidiInput input;
    private TimelinePlayer player;
    public TimelinePlayer Player => player;

    public VirtualTimelineGrid Timeline => pianoWithTimeline.Timeline;

    public INoteSource Notes => noteSource;
    public double BeatsPerMinute => noteSource.BeatsPerMinute;

    public MelodyMaker(IMidiInput input, double bpm = 60)
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input), "MIDI input cannot be null.");
        noteSource = new ListNoteSource() { BeatsPerMinute = bpm };
        player = new TimelinePlayer(() => noteSource.Select(n => n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : 0)).DefaultIfEmpty(0).Max(), noteSource.BeatsPerMinute)
        {
            StopAtEnd = false
        };
        pianoWithTimeline = ProtectedPanel.Add(new PianoWithTimeline(noteSource, player)).Fill();
        player.BeatChanged.Subscribe(pianoWithTimeline.Timeline, static (t,b) => t.RefreshVisibleSet(), this);
        input.EventFired.Subscribe(HandleMidiEvent, this);
        app = ConsoleApp.Current;
        Ready.SubscribeOnce(pianoWithTimeline.Timeline.Focus);
    }

    private void HandleMidiEvent(IMidiEvent ev)
    {
        if (IsNoteOn(ev))
        {
            HandleNoteOn(ev);
        }
        else if (IsNoteOff(ev) && noteTrackers.TryGetValue(ev.NoteNumber, out var tracker))
        {
            HandleNoteOff(ev, tracker);
        }
        else if(ev.Command == MidiCommand.NoteOff || ev.Command == MidiCommand.NoteOn)
        {
            app.WriteLine(ConsoleString.Parse($"[Red]Missed note off {ev.NoteNumber}"));
        }
    }

    private void HandleNoteOn(IMidiEvent ev)
    {
        if (noteTrackers.ContainsKey(ev.NoteNumber)) return;

        var noteExpression = NoteExpression.Create(ev.NoteNumber, player.CurrentBeat, -1, ev.Velocity, InstrumentExpression.Create("Keyboard", Timeline.InstrumentFactory));
        var voices = app.Sound.PlaySustainedNote(noteExpression);
        if (voices == null) return;

        Timeline.Player.StopAtEnd = false;
        player.Start(player.CurrentBeat);
        noteSource.Add(noteExpression);
        pianoWithTimeline.Timeline.RefreshVisibleSet();
        noteTrackers[ev.NoteNumber] = SustainedNoteTracker.Create(noteExpression, voices);
    }

    private void HandleNoteOff(IMidiEvent ev, SustainedNoteTracker tracker)
    {
        // Get the current timeline beat
        double playheadBeat = player.CurrentBeat;

        noteSource.Remove(tracker.Note);
        // Duration is from the note's start beat to the current playhead beat
        double duration = playheadBeat - tracker.Note.StartBeat;
        noteSource.Add(NoteExpression.Create(tracker.Note.MidiNote, tracker.Note.StartBeat, duration, tracker.Note.Velocity, tracker.Note.Instrument));
        tracker.ReleaseNote();
        noteTrackers.Remove(ev.NoteNumber);
    }

    public void StartPlayback()
    {
        pianoWithTimeline.Timeline.StartPlayback();
    }

    public static bool IsNoteOff(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOff || (midiEvent.Command == MidiCommand.NoteOn && midiEvent.Velocity == 0);
    public static bool IsNoteOn(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOn  && midiEvent.Velocity > 0;


}

public class SustainedNoteTracker : Recyclable
{
    private SustainedNoteTracker() { }
    private static LazyPool<SustainedNoteTracker> _pool = new(() => new SustainedNoteTracker());


    public NoteExpression Note { get; private set; }
    public IReleasableNote Releasable { get; private set; }
    NoteExpression NoteExpression { get; set; }
    public static SustainedNoteTracker Create(NoteExpression note, IReleasableNote releasable)
    {
        var tracker = _pool.Value.Rent();
        tracker.Note = note;
        tracker.Releasable = releasable;
        return tracker;
    }
    public void ReleaseNote()
    {
        Releasable.ReleaseNote();
        Dispose();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Note = null!;
        Releasable = null!;
    }
}