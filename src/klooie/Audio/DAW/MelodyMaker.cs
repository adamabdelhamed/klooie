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
    private SyncronousScheduler scheduler;
    public MelodyMaker(IMidiInput input)
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input), "MIDI input cannot be null.");
        noteSource = new ListNoteSource();
        pianoWithTimeline = ProtectedPanel.Add(new PianoWithTimeline(noteSource)).Fill();
        input.EventFired.Subscribe(HandleMidiEvent, this);
        app = ConsoleApp.Current;
        Ready.SubscribeOnce(pianoWithTimeline.Timeline.Focus);
        scheduler = new SyncronousScheduler(ConsoleApp.Current);
        scheduler.Mode = SyncronousScheduler.ExecutionMode.EndOfCycle; // vs. AfterPaint so that we get a tighter loop.
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
        if (noteSource.StartTimestamp == null)
        {
            noteSource.StartTimestamp = Stopwatch.GetTimestamp();
            RefreshLoop();
        }

        var elapsed = Stopwatch.GetElapsedTime(noteSource.StartTimestamp.Value);
        var elapsedAsBeats = (elapsed.TotalSeconds * noteSource.BeatsPerMinute / 60.0);
        var noteExpression = NoteExpression.Create(ev.NoteNumber, elapsedAsBeats, -1, ev.Velocity, null);
        var voices = app.Sound.PlaySustainedNote(noteExpression);
        noteSource.Add(noteExpression);
        pianoWithTimeline.Timeline.RefreshVisibleSet();
        noteTrackers[ev.NoteNumber] = new SustainedNoteTracker(noteExpression, voices);
    }

    private void HandleNoteOff(IMidiEvent ev, SustainedNoteTracker tracker)
    {
        var elapsed = Stopwatch.GetElapsedTime(noteSource.StartTimestamp.Value);
        var elapsedAsBeats = (elapsed.TotalSeconds * noteSource.BeatsPerMinute / 60.0);
        noteSource.Remove(tracker.Note);
        noteSource.Add(NoteExpression.Create(tracker.Note.MidiNote, tracker.Note.StartBeat, elapsedAsBeats - tracker.Note.StartBeat, noteSource.BeatsPerMinute, tracker.Note.Velocity, tracker.Note.Instrument));
        tracker.ReleaseAll();
        noteTrackers.Remove(ev.NoteNumber);
    }

    public static bool IsNoteOff(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOff || (midiEvent.Command == MidiCommand.NoteOn && midiEvent.Velocity == 0);
    public static bool IsNoteOn(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOn  && midiEvent.Velocity > 0;


    private void RefreshLoop()
    {
        pianoWithTimeline.Timeline.RefreshVisibleSet();
        scheduler.Delay(1, RefreshLoop);
    }
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