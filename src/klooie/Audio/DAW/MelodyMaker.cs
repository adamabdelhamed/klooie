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
        scheduler.Mode = SyncronousScheduler.ExecutionMode.EndOfCycle;
    }

    private void HandleMidiEvent(IMidiEvent ev)
    {
        if (IsNoteOn(ev))
        {
            if (noteTrackers.ContainsKey(ev.NoteNumber)) return;
            if(noteSource.StartTimestamp == null)
            {
                noteSource.StartTimestamp = Stopwatch.GetTimestamp();
                RefreshLoop();
            }

            var elapsed = Stopwatch.GetElapsedTime(noteSource.StartTimestamp.Value);
            var elapsedAsBeats = (elapsed.TotalSeconds * noteSource.BeatsPerMinute / 60.0);
            var noteExpression = NoteExpression.Create(ev.NoteNumber, elapsedAsBeats,  -1,  ev.Velocity,null);
            var note = Note.Create(ev.NoteNumber, ev.Velocity, SynthPatches.CreateGuitar().WithVolume(5));
            var voices = app.Sound.PlaySustainedNote(note);
            noteSource.Add(noteExpression);
            pianoWithTimeline.Timeline.RefreshVisibleSet();
            noteTrackers[ev.NoteNumber] = new SustainedNoteTracker(noteExpression, voices);
        }
        else if (IsNoteOff(ev) && noteTrackers.TryGetValue(ev.NoteNumber, out var tracker))
        {
            app.WriteLine(ConsoleString.Parse($"[Black]Note [Orange]{ev.NoteNumber}[Black] off"));

            var elapsed = Stopwatch.GetElapsedTime(noteSource.StartTimestamp.Value);
            var elapsedAsBeats = (elapsed.TotalSeconds * noteSource.BeatsPerMinute / 60.0);
            tracker.Note.DurationBeats = elapsedAsBeats - tracker.Note.StartBeat;

            tracker.ReleaseAll();
            noteTrackers.Remove(ev.NoteNumber);
        }
        else if(ev.Command == MidiCommand.NoteOff || ev.Command == MidiCommand.NoteOn)
        {
            app.WriteLine(ConsoleString.Parse($"[Red]Missed note off {ev.NoteNumber}"));
        }
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