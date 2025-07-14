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

    public MelodyMaker(IMidiInput input)
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input), "MIDI input cannot be null.");
        noteSource = new ListNoteSource();
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
        Timeline.Player.StopAtEnd = false;
        player.Start(player.CurrentBeat);
        var noteExpression = NoteExpression.Create(ev.NoteNumber, player.CurrentBeat, -1, ev.Velocity, InstrumentExpression.Create("Keyboard", Timeline.InstrumentFactory));
        var voices = app.Sound.PlaySustainedNote(noteExpression);
        noteSource.Add(noteExpression);
        pianoWithTimeline.Timeline.RefreshVisibleSet();
        noteTrackers[ev.NoteNumber] = new SustainedNoteTracker(noteExpression, voices);
    }

    private void HandleNoteOff(IMidiEvent ev, SustainedNoteTracker tracker)
    {
        // Get the current timeline beat
        double playheadBeat = player.CurrentBeat;

        noteSource.Remove(tracker.Note);
        // Duration is from the note's start beat to the current playhead beat
        double duration = playheadBeat - tracker.Note.StartBeat;
        noteSource.Add(NoteExpression.Create(tracker.Note.MidiNote, tracker.Note.StartBeat, duration, tracker.Note.Velocity, tracker.Note.Instrument));
        tracker.ReleaseAll();
        noteTrackers.Remove(ev.NoteNumber);
    }

    public void StartPlayback()
    {
        pianoWithTimeline.Timeline.StartPlayback();
    }

    public static bool IsNoteOff(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOff || (midiEvent.Command == MidiCommand.NoteOn && midiEvent.Velocity == 0);
    public static bool IsNoteOn(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOn  && midiEvent.Velocity > 0;


}

public class SustainedNoteTracker
{
    public NoteExpression Note { get; }
    public RecyclableList<IReleasableNote> Voices { get; }
    NoteExpression NoteExpression { get; set; }
    public SustainedNoteTracker(NoteExpression note, RecyclableList<IReleasableNote> voices)
    {
        this.Note = note;
        Voices = voices;
    }
    public void ReleaseAll()
    {
        foreach (var voice in Voices.Items)
        {
            voice.ReleaseNote();
        }
        Voices.Dispose();
    }
}