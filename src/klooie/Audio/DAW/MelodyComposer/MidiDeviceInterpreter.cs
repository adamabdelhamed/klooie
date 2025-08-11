using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;
public class MidiDeviceInterpretor : Recyclable
{
    private IMidiInput midiInput;
    private Dropdown midiDropdown;
    private IMidiProvider midiImpl;
    private MidiNoteOnOffDetector noteDetector;
    private MelodyComposer melodyComposer;
    private MidiLiveNoteEngine engine;
    private MidiDeviceInterpretor() { }
    private static LazyPool<MidiDeviceInterpretor> lazyPool = new(() => new MidiDeviceInterpretor());

    public static MidiDeviceInterpretor Create(IMidiProvider midiImpl, MelodyComposer melodyComposer)
    {
        var instance = lazyPool.Value.Rent();
        instance.melodyComposer = melodyComposer ?? throw new ArgumentNullException(nameof(melodyComposer));
        instance.midiImpl = midiImpl ?? throw new ArgumentNullException(nameof(midiImpl));
        instance.engine = MidiLiveNoteEngine.Create((noteNumber, velocity) => NoteExpression.Create(noteNumber, instance.melodyComposer.Grid.Player.CurrentBeat, -1, WorkspaceSession.Current.CurrentSong.BeatsPerMinute, velocity, instance.melodyComposer.Grid.Instrument));
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
        if (!engine.TryStart(ev.NoteNumber, ev.Velocity, out var noteExpression, out var tracker)) return;

        melodyComposer.Grid.Player.StopAtEnd = false;
        melodyComposer.Grid.Player.Play();
        melodyComposer.Grid.Notes.Add(noteExpression);
        melodyComposer.Grid.EnsureMidiVisible(noteExpression.MidiNote);
        melodyComposer.Grid.RefreshVisibleCells();
    }

    private void HandleNoteOff(int noteNumber)
    {
        if (!engine.TryStop(noteNumber, out var tempNote, out var tracker)) return;

        double playheadBeat = melodyComposer.Grid.Player.CurrentBeat;
        double snappedStart = SnapToGrid(tempNote.StartBeat);
        double snappedEnd = SnapToGrid(playheadBeat);
        if (snappedEnd <= snappedStart) snappedEnd = snappedStart + melodyComposer.Grid.BeatsPerColumn;

        double duration = snappedEnd - snappedStart;
        melodyComposer.Grid.Notes.Remove(tempNote);
        var completedNote = NoteExpression.Create(tempNote.MidiNote, snappedStart, duration,  tempNote.Velocity, tempNote.Instrument);
        AudioPreRenderer.Instance.Queue(completedNote);
        WorkspaceSession.Current.Commands.Execute(new AddNoteCommand(melodyComposer.Grid, completedNote));
        tracker.ReleaseNote();
        melodyComposer.Grid.RefreshVisibleCells();
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

        engine?.ReleaseAll();
        engine?.TryDispose();
        engine = null!;

        melodyComposer = null!;
        midiDropdown?.TryDispose();
        midiDropdown = null;
    }
}