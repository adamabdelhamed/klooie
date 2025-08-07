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
    private IMidiProvider midiImpl;
    private MidiNoteOnOffDetector noteDetector;
    private Dictionary<int, SustainedNoteTracker> noteTrackers = new Dictionary<int, SustainedNoteTracker>();
    private MelodyComposer pianoWithTimeline;
    private DAWMidi() { }
    private static LazyPool<DAWMidi> lazyPool = new(() => new DAWMidi());

    public static DAWMidi Create(IMidiProvider midiImpl, MelodyComposer pianoWithTimeline)
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

        var noteExpression = NoteExpression.Create(ev.NoteNumber, pianoWithTimeline.Grid.Player.CurrentBeat, -1, ev.Velocity, pianoWithTimeline.Grid.Instrument);
        var voices = ConsoleApp.Current.Sound.PlaySustainedNote(noteExpression);
        if (voices == null) return;

        pianoWithTimeline.Grid.Player.StopAtEnd = false;
        pianoWithTimeline.Grid.Player.Play();
        pianoWithTimeline.Grid.Notes.Add(noteExpression);
        pianoWithTimeline.Grid.RefreshVisibleCells();
        noteTrackers[ev.NoteNumber] = SustainedNoteTracker.Create(noteExpression, voices);
    }

    private void HandleNoteOff(int noteNumber)
    {
        if (!noteTrackers.TryGetValue(noteNumber, out var tracker)) return;

        double playheadBeat = pianoWithTimeline.Grid.Player.CurrentBeat;
        double snappedStart = SnapToGrid(tracker.Note.StartBeat);
        double snappedEnd = SnapToGrid(playheadBeat);
        if (snappedEnd <= snappedStart) snappedEnd = snappedStart + pianoWithTimeline.Grid.BeatsPerColumn;

        double duration = snappedEnd - snappedStart;
        pianoWithTimeline.Grid.Notes.Remove(tracker.Note);
        WorkspaceSession.Current.Commands.Execute(new AddNoteCommand(pianoWithTimeline.Grid, NoteExpression.Create(tracker.Note.MidiNote, snappedStart, duration, tracker.Note.Velocity, tracker.Note.Instrument)));
        tracker.ReleaseNote();
        noteTrackers.Remove(noteNumber);
    }


    private double SnapToGrid(double beat)
    {
        double grid = 0.125; 
        return Math.Round(beat / grid) * grid;
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