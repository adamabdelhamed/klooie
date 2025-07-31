using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DAWMidi : Recyclable
{
    private IMidiInput midiInput;
    private Dropdown midiDropdown;
    private IMidiProductDiscoverer midiImpl;
    private MidiNoteOnOffDetector noteDetector;
    private Dictionary<int, SustainedNoteTracker> noteTrackers = new Dictionary<int, SustainedNoteTracker>();
    private PianoWithTimeline pianoWithTimeline;
    private DAWMidi() { }
    private static LazyPool<DAWMidi> lazyPool = new(() => new DAWMidi());

    public static DAWMidi Create(IMidiProductDiscoverer midiImpl, PianoWithTimeline pianoWithTimeline)
    {
        var instance = lazyPool.Value.Rent();
        instance.pianoWithTimeline = pianoWithTimeline ?? throw new ArgumentNullException(nameof(pianoWithTimeline));
        instance.midiImpl = midiImpl ?? throw new ArgumentNullException(nameof(midiImpl));
        return instance;
    }

    private void BindToMidiProduct()
    {
        if (midiImpl.TryConnect(midiDropdown.Value.Id, out IMidiInput input))
        {
            noteDetector?.Dispose();
            noteDetector = MidiNoteOnOffDetector.Create(input);
            noteDetector.NoteOn.Subscribe(HandleNoteOn, noteDetector);
            noteDetector.NoteOff.Subscribe(HandleNoteOff, noteDetector);
            midiInput = input;
        }
        else
        {
            midiInput = null;
        }
    }

    private void HandleNoteOn((int NoteNumber, int Velocity) ev)
    {
        if (noteTrackers.ContainsKey(ev.NoteNumber)) return;

        var noteExpression = NoteExpression.Create(ev.NoteNumber, pianoWithTimeline.Timeline.TimelinePlayer.CurrentBeat, -1, ev.Velocity, InstrumentExpression.Create("Keyboard", pianoWithTimeline.Timeline.InstrumentFactory));
        var voices = ConsoleApp.Current.Sound.PlaySustainedNote(noteExpression);
        if (voices == null) return;

        pianoWithTimeline.Timeline.TimelinePlayer.StopAtEnd = false;
        pianoWithTimeline.Timeline.TimelinePlayer.Start(pianoWithTimeline.Timeline.TimelinePlayer.CurrentBeat);
        pianoWithTimeline.Timeline.Notes.Add(noteExpression);
        pianoWithTimeline.Timeline.RefreshVisibleSet();
        noteTrackers[ev.NoteNumber] = SustainedNoteTracker.Create(noteExpression, voices);
    }

    private void HandleNoteOff(int noteNumber)
    {
        if (!noteTrackers.TryGetValue(noteNumber, out var tracker)) return;

        double playheadBeat = pianoWithTimeline.Timeline.TimelinePlayer.CurrentBeat;
        pianoWithTimeline.Timeline.Notes.Remove(tracker.Note);
        double duration = playheadBeat - tracker.Note.StartBeat;
        WorkspaceSession.Current.Commands.Execute(new AddNoteCommand( pianoWithTimeline.Timeline.Notes, pianoWithTimeline.Timeline, NoteExpression.Create(tracker.Note.MidiNote, tracker.Note.StartBeat, duration, tracker.Note.Velocity, tracker.Note.Instrument),pianoWithTimeline.Timeline.SelectedNotes));
        tracker.ReleaseNote();
        noteTrackers.Remove(noteNumber);
    }

    public Dropdown CreateMidiProductDropdown()
    {
        midiDropdown = new Dropdown(midiImpl.GetProductNames().Select(p => new DialogChoice() { DisplayText = p.ToConsoleString(), Id = p }));
        midiDropdown.ValueChanged.Sync(BindToMidiProduct, midiDropdown);
        return midiDropdown;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        midiInput = null;
        noteDetector?.Dispose();
        noteDetector = null;
        noteTrackers.Clear();
        pianoWithTimeline = null!;
        midiDropdown?.TryDispose();
        midiDropdown = null;
    }
}