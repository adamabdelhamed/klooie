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
    public InstrumentExpression Instrument { get; set; } = new InstrumentExpression() { Name = "Default", PatchFunc = SynthLead.Create };
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
}
