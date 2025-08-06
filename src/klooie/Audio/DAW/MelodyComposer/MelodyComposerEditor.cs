using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class MelodyComposerEditor
{
    public required MelodyComposer Composer { get; init; }
    private readonly List<NoteExpression> clipboard = new();

    private ConsoleControl? addNotePreview;
    private (double Start, double Duration, int Midi)? pendingAddNote;

    private CommandStack CommandStack { get; init; }

    public MelodyComposerEditor(CommandStack commandStack)
    {
        this.CommandStack = commandStack;
    }

    public bool HandleKeyInput(ConsoleKeyInfo k)
    {
        bool Matches(ConsoleKey key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            return k.Key == key
                && (!ctrl || k.Modifiers.HasFlag(ConsoleModifiers.Control))
                && (!shift || k.Modifiers.HasFlag(ConsoleModifiers.Shift))
                && (!alt || k.Modifiers.HasFlag(ConsoleModifiers.Alt))
                && (ctrl ? k.Modifiers.HasFlag(ConsoleModifiers.Control) : !k.Modifiers.HasFlag(ConsoleModifiers.Control))
                && (shift ? k.Modifiers.HasFlag(ConsoleModifiers.Shift) : !k.Modifiers.HasFlag(ConsoleModifiers.Shift))
                && (alt ? k.Modifiers.HasFlag(ConsoleModifiers.Alt) : !k.Modifiers.HasFlag(ConsoleModifiers.Alt));
        }

        // SELECTION
        if (Matches(ConsoleKey.A, ctrl: true)) return SelectAll();
        if (Matches(ConsoleKey.D, ctrl: true)) return DeselectAll();
        if (Matches(ConsoleKey.LeftArrow, ctrl: true) || Matches(ConsoleKey.RightArrow, ctrl: true)) return SelectAllLeftOrRight(k);

        // CLIPBOARD
        if (Matches(ConsoleKey.C, shift: true)) return Copy();
        if (Matches(ConsoleKey.V, shift: true)) return Paste();

        // DELETE
        if (Matches(ConsoleKey.Delete)) return DeleteSelected();

        // MOVE
        if (Matches(ConsoleKey.LeftArrow, alt: true) || Matches(ConsoleKey.RightArrow, alt: true) || Matches(ConsoleKey.UpArrow, alt: true) || Matches(ConsoleKey.DownArrow, alt: true)) return MoveSelection(k);

        // VELOCITY
        if (Matches(ConsoleKey.UpArrow, shift: true) || Matches(ConsoleKey.W, shift: true)) return AdjustVelocity(1);
        if (Matches(ConsoleKey.DownArrow, shift: true) || Matches(ConsoleKey.S, shift: true)) return AdjustVelocity(-1);

        // DURATION
        if (Matches(ConsoleKey.LeftArrow, shift: true) || Matches(ConsoleKey.A, shift: true)) return AdjustDuration(-Composer.BeatsPerColumn);
        if (Matches(ConsoleKey.RightArrow, shift: true) || Matches(ConsoleKey.D, shift: true)) return AdjustDuration(Composer.BeatsPerColumn);

        // UNDO/REDO
        if (Matches(ConsoleKey.Z, ctrl: true)) return Undo();
        if (Matches(ConsoleKey.Y, ctrl: true)) return Redo();

        // ADD NOTE PREVIEW
        if (Matches(ConsoleKey.P) && pendingAddNote != null) return CommitAddNote();
        if (Matches(ConsoleKey.D, alt: true) && pendingAddNote != null) return DismissAddNotePreview();

        return false;
    }

    // --- SELECTION ---
    private bool SelectAll()
    {
        Composer.SelectedNotes.Clear();
        Composer.SelectedNotes.AddRange(Composer.Notes);
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire("All notes selected".ToWhite());
        return true;
    }
    private bool DeselectAll()
    {
        Composer.SelectedNotes.Clear();
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire("Deselected all notes".ToWhite());
        return true;
    }
    private bool SelectAllLeftOrRight(ConsoleKeyInfo k)
    {
        var left = k.Key == ConsoleKey.LeftArrow;
        Composer.SelectedNotes.Clear();
        Composer.SelectedNotes.AddRange(Composer.Notes.Where(n =>
            (left && n.StartBeat <= Composer.CurrentBeat) ||
            (!left && n.StartBeat >= Composer.CurrentBeat)));
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire("All notes selected".ToWhite());
        return true;
    }

    // --- CLIPBOARD ---
    private bool Copy()
    {
        clipboard.Clear();
        Composer.StatusChanged.Fire($"Copied {Composer.SelectedNotes.Count} notes to clipboard".ToWhite());
        clipboard.AddRange(Composer.SelectedNotes);
        return true;
    }
    private bool Paste()
    {
        if (Composer.Notes is not ListNoteSource) return true;
        if (clipboard.Count == 0) return true;
        double offset = Composer.CurrentBeat - clipboard.Min(n => n.StartBeat);

        var pasted = new List<NoteExpression>();
        var addCmds = new List<ICommand>();

        foreach (var n in clipboard)
        {
            var nn = NoteExpression.Create(n.MidiNote,  Math.Max(0, n.StartBeat + offset), n.DurationBeats,  Composer.Notes.BeatsPerMinute,  n.Velocity,  n.Instrument);
            pasted.Add(nn);
            addCmds.Add(new AddNoteCommand(Composer, nn));
        }

        CommandStack.Execute(new MultiCommand(addCmds, "Paste Notes"));
        Composer.SelectedNotes.Clear();
        Composer.SelectedNotes.AddRange(pasted);
        return true;
    }

    // --- DELETE ---
    private bool DeleteSelected()
    {
        if (Composer.Notes is not ListNoteSource) return true;
        if (Composer.SelectedNotes.Count == 0) return true;

        var deleteCmds = Composer.SelectedNotes
            .Select(note => new DeleteNoteCommand(Composer, note))
            .ToList<ICommand>();

        CommandStack.Execute(new MultiCommand(deleteCmds, "Delete Selected Notes"));
        return true;
    }

    // --- MOVE ---
    private bool MoveSelection(ConsoleKeyInfo k)
    {
        if (Composer.Notes is not ListNoteSource list) return true;
        if (Composer.SelectedNotes.Count == 0) return true;

        double beatDelta = 0;
        int midiDelta = 0;
        if (k.Key == ConsoleKey.LeftArrow) beatDelta = -Composer.BeatsPerColumn;
        else if (k.Key == ConsoleKey.RightArrow) beatDelta = Composer.BeatsPerColumn;
        else if (k.Key == ConsoleKey.UpArrow) midiDelta = 1;
        else if (k.Key == ConsoleKey.DownArrow) midiDelta = -1;

        var updated = new List<NoteExpression>();
        var moveCmds = new List<ICommand>();

        foreach (var n in Composer.SelectedNotes)
        {
            int newMidi = Math.Clamp(n.MidiNote + midiDelta, 0, 127);
            double newBeat = Math.Max(0, n.StartBeat + beatDelta);
            var nn = NoteExpression.Create(newMidi, newBeat, n.DurationBeats, Composer.Notes.BeatsPerMinute, n.Velocity, n.Instrument);
            updated.Add(nn);
            moveCmds.Add(new ChangeNoteCommand(Composer, n, nn));
        }

        if (moveCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(moveCmds, "Move Notes"));
        }
        return true;
    }

    // --- VELOCITY ---
    private bool AdjustVelocity(int delta)
    {
        if (Composer.SelectedNotes.Count == 0) return true;

        var updated = new List<NoteExpression>();
        var velCmds = new List<ICommand>();

        foreach (var n in Composer.SelectedNotes)
        {
            int newVel = Math.Clamp(n.Velocity + delta, 1, 127);
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, n.DurationBeats, Composer.Notes.BeatsPerMinute, newVel, n.Instrument);
            updated.Add(nn);
            velCmds.Add(new ChangeNoteCommand(Composer, n, nn));
        }

        if (velCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(velCmds, "Change Velocity"));
        }
        return true;
    }

    // --- DURATION ---
    private bool AdjustDuration(double deltaBeats)
    {
        if (Composer.SelectedNotes.Count == 0) return true;

        var updated = new List<NoteExpression>();
        var durCmds = new List<ICommand>();

        foreach (var n in Composer.SelectedNotes)
        {
            double newDuration = Math.Max(0.1, n.DurationBeats + deltaBeats); // Don't allow zero or negative duration
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, newDuration, Composer.Notes.BeatsPerMinute, n.Velocity, n.Instrument);
            updated.Add(nn);
            durCmds.Add(new ChangeNoteCommand(Composer, n, nn));
        }

        if (durCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(durCmds, "Change Duration"));
        }
        return true;
    }

    // --- UNDO/REDO ---
    private bool Undo()
    {
        CommandStack.Undo();
        return true;
    }
    private bool Redo()
    {
        CommandStack.Redo();
        return true;
    }

    // --- ADD NOTE PREVIEW ---
    public void BeginAddNotePreview(double start, double duration, int midi)
    {
        ClearAddNotePreview();
        pendingAddNote = (start, duration, midi);
        addNotePreview = Composer.AddPreviewControl();
        addNotePreview.Background = RGB.DarkGreen;
        addNotePreview.ZIndex = 0;
        Composer.Viewport.Changed.Subscribe(addNotePreview, _ => PositionAddNotePreview(), addNotePreview);
        PositionAddNotePreview();
        Composer.StatusChanged.Fire(ConsoleString.Parse("[White]Press [Cyan]p[White] to add a note here or press ALT + D to deselect."));
    }
    public void PositionAddNotePreview()
    {
        if (pendingAddNote == null || addNotePreview == null) return;
        var (start, duration, midi) = pendingAddNote.Value;
        int x = ConsoleMath.Round((start - Composer.Viewport.FirstVisibleBeat) / Composer.BeatsPerColumn) * MelodyComposer.ColWidthChars;
        int y = (Composer.Viewport.FirstVisibleRow + Composer.Viewport.RowsOnScreen - 1 - midi) * MelodyComposer.RowHeightChars;
        int w = Math.Max(1, ConsoleMath.Round(duration / Composer.BeatsPerColumn) * MelodyComposer.ColWidthChars);
        int h = MelodyComposer.RowHeightChars;
        addNotePreview.MoveTo(x, y);
        addNotePreview.ResizeTo(w, h);
    }
    public void ClearAddNotePreview()
    {
        addNotePreview?.Dispose();
        addNotePreview = null;
        pendingAddNote = null;
    }
    private bool CommitAddNote()
    {
        if (pendingAddNote == null) return true;
        var (start, duration, midi) = pendingAddNote.Value;
        var command = new AddNoteCommand(Composer, NoteExpression.Create(midi, start, duration, Composer.Notes.BeatsPerMinute, instrument: Composer.Instrument));
        Composer.Session.Commands.Execute(command);
        return true;
    }
    private bool DismissAddNotePreview()
    {
        ClearAddNotePreview();
        Composer.RefreshVisibleSet();
        return true;
    }
}
