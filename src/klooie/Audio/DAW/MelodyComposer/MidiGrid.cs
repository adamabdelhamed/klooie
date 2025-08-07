using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class MidiGrid : BeatGrid<NoteExpression>
{
    public MidiGridEditor Editor { get; }
    public InstrumentExpression Instrument { get; set; } = InstrumentPicker.GetAllKnownInstruments().First();
    public override bool IsNavigating => CurrentMode is MidiGridNavigator;
    protected override BeatGridInputMode<NoteExpression>[] GetAvailableModes() => [new MidiGridNavigator() { Composer = this }, new MidiGridSelector() { Composer = this }];

    public RGB Color { get; set; } = RGB.Magenta;

    public ListNoteSource Notes { get; private set; }

    protected override IEnumerable<NoteExpression> EnumerateValues() => Notes;

    public MidiGrid(WorkspaceSession session, ListNoteSource notes) : base(session, notes.BeatsPerMinute)
    {
        this.Notes = notes;
        Editor = new MidiGridEditor(this, session.Commands);
        Viewport.Changed.Subscribe(() => (CurrentMode as MidiGridSelector)?.SyncCursorToCurrentZoom(), this);
        Refreshed.Subscribe(Editor.PositionAddPreview, this);
        if(notes.Count > 0)
        {
            ConsoleApp.Current.InvokeNextCycle(()=> EnsureMidiVisible(notes[0].MidiNote));
        }
    }

    public override void HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.M)
        {
            NextMode();
        }
        else if (!Editor.HandleKeyInput(k))
        {
            CurrentMode.HandleKeyInput(k);
        }
    }

    private double GetSustainedNoteDurationBeats(NoteExpression n)
    {
        double sustainedBeats = Player.CurrentBeat - n.StartBeat;
        return Math.Max(0, sustainedBeats);
    }

    protected override CellPositionInfo GetCellPositionInfo(NoteExpression value) => new CellPositionInfo()
    {
        BeatStart = value.StartBeat,
        BeatEnd = value.StartBeat + (value.DurationBeats > 0 ? value.DurationBeats : GetSustainedNoteDurationBeats(value)),
        IsHidden = value.Velocity <= 0,
        Row = 127 - value.MidiNote,
    };

    protected override double CalculateMaxBeat() => Notes.Select(n => n.StartBeat + n.DurationBeats).DefaultIfEmpty(0).Max();
    protected override RGB GetColor(NoteExpression note) => Color;
    public override Song Compose() => new Song(Notes, BeatsPerMinute);

    internal void EnsureMidiVisible(int midiNote)
    {
        int row = 127 - midiNote;
        int first = Viewport.FirstVisibleRow;
        int last = first + Viewport.RowsOnScreen - 1;

        if (row < first || row > last)
        {
            // Try to center the row in the viewport
            int centeredFirst = row - (Viewport.RowsOnScreen / 2);
            // Clamp to valid range: [0, 127 - (RowsOnScreen - 1)]
            int maxFirst = Math.Max(0, 127 - (Viewport.RowsOnScreen - 1));
            centeredFirst = Math.Max(0, Math.Min(centeredFirst, maxFirst));
            Viewport.SetFirstVisibleRow(centeredFirst);
        }
        // else already visible, do nothing
    }


}
