using klooie;
using System;
using System.Collections.Generic;
using System.Linq;

public class MidiGridEditor : BaseGridEditor<MidiGrid, NoteExpression>
{

    public MidiGrid grid;
    protected override MidiGrid Grid => grid;
    private (double Start, double Duration, int Midi)? pendingAddNote;
    private ConsoleControl? addNotePreview;

    public MidiGridEditor(MidiGrid grid, CommandStack commandStack) : base(commandStack) 
    {
        this.grid = grid;
    }

    public override bool HandleKeyInput(ConsoleKeyInfo k)
    {
        // MIDI-specific keys
        if (Matches(k, ConsoleKey.UpArrow, shift: true) || Matches(k, ConsoleKey.W, shift: true))
            return AdjustVelocity(1);
        if (Matches(k, ConsoleKey.DownArrow, shift: true) || Matches(k, ConsoleKey.S, shift: true))
            return AdjustVelocity(-1);
        if (Matches(k, ConsoleKey.LeftArrow, shift: true) || Matches(k, ConsoleKey.A, shift: true))
            return AdjustDuration(-GetDurationStep());
        if (Matches(k, ConsoleKey.RightArrow, shift: true) || Matches(k, ConsoleKey.D, shift: true))
            return AdjustDuration(GetDurationStep());
        if (Matches(k, ConsoleKey.D, shift: true))
            return DuplicateSelected();

        // Add-preview for MIDI
        if (Matches(k, ConsoleKey.P) && pendingAddNote != null) return CommitAddPreview();
        if (Matches(k, ConsoleKey.D, alt: true) && pendingAddNote != null) return DismissAddPreview();

        // Not handled? Pass to base.
        return base.HandleKeyInput(k);
    }

    protected override List<NoteExpression> GetSelectedValues() => Grid.SelectedValues;
    protected override List<NoteExpression> GetAllValues() => Grid.Values;
    protected override void RefreshVisibleCells() => Grid.RefreshVisibleCells();
    protected override void FireStatusChanged(ConsoleString msg) => Grid.StatusChanged.Fire(msg);

    protected override bool SelectAllLeftOrRight(ConsoleKeyInfo k)
    {
        var left = k.Key == ConsoleKey.LeftArrow;
        var sel = GetSelectedValues();
        sel.Clear();
        sel.AddRange(Grid.Values.Where(n =>
            (left && n.StartBeat <= Grid.Player.CurrentBeat) ||
            (!left && n.StartBeat >= Grid.Player.CurrentBeat)));
        RefreshVisibleCells();
        FireStatusChanged("All notes selected".ToWhite());
        return true;
    }

    protected override IEnumerable<NoteExpression> DeepCopyClipboard(IEnumerable<NoteExpression> src)
        => src.Select(n => NoteExpression.Create(n.MidiNote, n.StartBeat, n.DurationBeats, n.BeatsPerMinute, n.Velocity, n.Instrument)).ToList();

    protected override bool PasteClipboard()
    {
        if (Grid.Values is not ListNoteSource) return true;
        if (Clipboard.Count == 0) return true;
        double offset = Grid.Player.CurrentBeat - Clipboard.Min(n => n.StartBeat);

        var pasted = new List<NoteExpression>();
        var addCmds = new List<ICommand>();

        foreach (var n in Clipboard)
        {
            var nn = NoteExpression.Create(n.MidiNote, Math.Max(0, n.StartBeat + offset), n.DurationBeats, n.BeatsPerMinute, n.Velocity, n.Instrument);
            pasted.Add(nn);
            addCmds.Add(new AddNoteCommand(Grid, nn));
        }

        CommandStack.Execute(new MultiCommand(addCmds, "Paste Notes"));
        Grid.SelectedValues.Clear();
        Grid.SelectedValues.AddRange(pasted);
        return true;
    }

    protected override bool DeleteSelected()
    {
        if (Grid.Values is not ListNoteSource) return true;
        if (Grid.SelectedValues.Count == 0) return true;

        var deleteCmds = Grid.SelectedValues
            .Select(note => new DeleteNoteCommand(Grid, note))
            .ToList<ICommand>();

        CommandStack.Execute(new MultiCommand(deleteCmds, "Delete Selected Notes"));
        return true;
    }

    protected override bool MoveSelection(ConsoleKeyInfo k)
    {
        if (Grid.Values is not ListNoteSource list) return true;
        if (Grid.SelectedValues.Count == 0) return true;

        double beatDelta = 0;
        int midiDelta = 0;
        if (k.Key == ConsoleKey.LeftArrow) beatDelta = -Grid.BeatsPerColumn;
        else if (k.Key == ConsoleKey.RightArrow) beatDelta = Grid.BeatsPerColumn;
        else if (k.Key == ConsoleKey.UpArrow) midiDelta = 1;
        else if (k.Key == ConsoleKey.DownArrow) midiDelta = -1;

        var updated = new List<NoteExpression>();
        var moveCmds = new List<ICommand>();

        foreach (var n in Grid.SelectedValues)
        {
            int newMidi = Math.Clamp(n.MidiNote + midiDelta, 0, 127);
            double newBeat = Math.Max(0, n.StartBeat + beatDelta);
            var nn = NoteExpression.Create(newMidi, newBeat, n.DurationBeats, n.BeatsPerMinute, n.Velocity, n.Instrument);
            updated.Add(nn);
            moveCmds.Add(new ChangeNoteCommand(Grid, n, nn));
        }

        if (moveCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(moveCmds, "Move Notes"));
        }
        return true;
    }

    public bool AdjustVelocity(int delta)
    {
        if (Grid.SelectedValues.Count == 0) return true;

        var updated = new List<NoteExpression>();
        var velCmds = new List<ICommand>();

        foreach (var n in Grid.SelectedValues)
        {
            int newVel = Math.Clamp(n.Velocity + delta, 1, 127);
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, n.DurationBeats, n.BeatsPerMinute, newVel, n.Instrument);
            updated.Add(nn);
            velCmds.Add(new ChangeNoteCommand(Grid, n, nn));
        }

        if (velCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(velCmds, "Change Velocity"));
        }
        return true;
    }

    public bool AdjustDuration(double deltaBeats)
    {
        if (Grid.SelectedValues.Count == 0) return true;

        var updated = new List<NoteExpression>();
        var durCmds = new List<ICommand>();

        foreach (var n in Grid.SelectedValues)
        {
            double newDuration = Math.Max(0.1, n.DurationBeats + deltaBeats); // Don't allow zero or negative duration
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, newDuration, n.BeatsPerMinute, n.Velocity, n.Instrument);
            updated.Add(nn);
            durCmds.Add(new ChangeNoteCommand(Grid, n, nn));
        }

        if (durCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(durCmds, "Change Duration"));
        }
        return true;
    }

    public double GetDurationStep() => Grid.BeatsPerColumn;

    public bool DuplicateSelected()
    {
        if (Grid.SelectedValues.Count == 0) return true;

        var duplicates = new List<NoteExpression>();
        var addCmds = new List<ICommand>();

        foreach (var n in Grid.SelectedValues)
        {
            var dup = NoteExpression.Create(n.MidiNote, n.StartBeat + n.DurationBeats, n.DurationBeats, n.BeatsPerMinute, n.Velocity, n.Instrument);
            duplicates.Add(dup);
            addCmds.Add(new AddNoteCommand(Grid, dup));
        }
        CommandStack.Execute(new MultiCommand(addCmds, "Duplicate Notes"));
        Grid.SelectedValues.Clear();
        Grid.SelectedValues.AddRange(duplicates);
        RefreshVisibleCells();
        FireStatusChanged($"Duplicated {duplicates.Count} notes".ToWhite());
        return true;
    }

    // --- PREVIEW ADD (Midi only) ---
    public void BeginAddPreview(double start, double duration, int midi)
    {
        ClearAddPreview();
        pendingAddNote = (start, duration, midi);
        addNotePreview = Grid.AddPreviewControl();
        addNotePreview.Background = RGB.DarkGreen;
        addNotePreview.ZIndex = 0;
        Grid.Viewport.Changed.Subscribe(addNotePreview, _ => PositionAddPreview(), addNotePreview);
        PositionAddPreview();
        FireStatusChanged(ConsoleString.Parse("[White]Press [Cyan]p[White] to add a note here or press ALT + D to deselect."));
    }

    public void PositionAddPreview()
    {
        if (pendingAddNote == null || addNotePreview == null) return;
        var (start, duration, midi) = pendingAddNote.Value;
        int x = ConsoleMath.Round((start - Grid.Viewport.FirstVisibleBeat) / Grid.BeatsPerColumn) * Grid.Viewport.ColWidthChars;
        int y = (Grid.Viewport.FirstVisibleRow + Grid.Viewport.RowsOnScreen - 1 - midi) * Grid.Viewport.RowHeightChars;
        int w = Math.Max(1, ConsoleMath.Round(duration / Grid.BeatsPerColumn) * Grid.Viewport.ColWidthChars);
        int h = Grid.Viewport.RowHeightChars;
        addNotePreview.MoveTo(x, y);
        addNotePreview.ResizeTo(w, h);
    }

    public void ClearAddPreview()
    {
        addNotePreview?.Dispose();
        addNotePreview = null;
        pendingAddNote = null;
    }

    private bool CommitAddPreview()
    {
        if (pendingAddNote == null) return true;
        var (start, duration, midi) = pendingAddNote.Value;
        var bpm = (Grid.Values as ListNoteSource).BeatsPerMinute;
        var command = new AddNoteCommand(Grid, NoteExpression.Create(midi, start, duration, bpm, instrument: Grid.Instrument));
        Grid.Session.Commands.Execute(command);
        ClearAddPreview();
        return true;
    }

    private bool DismissAddPreview()
    {
        ClearAddPreview();
        RefreshVisibleCells();
        return true;
    }
}
