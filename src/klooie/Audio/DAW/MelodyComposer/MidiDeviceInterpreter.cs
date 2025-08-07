using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MidiDeviceInterpretor : Recyclable
{
    private IMidiInput midiInput;
    private Dropdown midiDropdown;
    private IMidiProvider midiImpl;
    private MidiNoteOnOffDetector noteDetector;
    private Dictionary<int, SustainedNoteTracker> noteTrackers = new Dictionary<int, SustainedNoteTracker>();
    private MelodyComposer melodyComposer;
    private MidiDeviceInterpretor() { }
    private static LazyPool<MidiDeviceInterpretor> lazyPool = new(() => new MidiDeviceInterpretor());

    public static MidiDeviceInterpretor Create(IMidiProvider midiImpl, MelodyComposer melodyComposer)
    {
        var instance = lazyPool.Value.Rent();
        instance.melodyComposer = melodyComposer ?? throw new ArgumentNullException(nameof(melodyComposer));
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

        var noteExpression = NoteExpression.Create(ev.NoteNumber, melodyComposer.Grid.Player.CurrentBeat, -1, ev.Velocity, melodyComposer.Grid.Instrument);
        var voices = ConsoleApp.Current.Sound.PlaySustainedNote(noteExpression);
        if (voices == null) return;

        melodyComposer.Grid.Player.StopAtEnd = false;
        melodyComposer.Grid.Player.Play();
        melodyComposer.Grid.Notes.Add(noteExpression);
        melodyComposer.Grid.EnsureMidiVisible(noteExpression.MidiNote);
        melodyComposer.Grid.RefreshVisibleCells();
        noteTrackers[ev.NoteNumber] = SustainedNoteTracker.Create(noteExpression, voices);
    }

    private void HandleNoteOff(int noteNumber)
    {
        if (!noteTrackers.TryGetValue(noteNumber, out var tracker)) return;

        double playheadBeat = melodyComposer.Grid.Player.CurrentBeat;
        double snappedStart = SnapToGrid(tracker.Note.StartBeat);
        double snappedEnd = SnapToGrid(playheadBeat);
        if (snappedEnd <= snappedStart) snappedEnd = snappedStart + melodyComposer.Grid.BeatsPerColumn;

        double duration = snappedEnd - snappedStart;
        melodyComposer.Grid.Notes.Remove(tracker.Note);
        WorkspaceSession.Current.Commands.Execute(new AddNoteCommand(melodyComposer.Grid, NoteExpression.Create(tracker.Note.MidiNote, snappedStart, duration, tracker.Note.Velocity, tracker.Note.Instrument)));
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
        melodyComposer = null!;
        midiDropdown?.TryDispose();
        midiDropdown = null;
    }
}