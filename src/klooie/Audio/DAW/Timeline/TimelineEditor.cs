using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class TimelineEditor
{
    public required VirtualTimelineGrid Timeline { get; init; }
    private readonly List<NoteExpression> clipboard = new();

    private ConsoleControl? addNotePreview;
    private (double Start, double Duration, int Midi)? pendingAddNote;

    private CommandStack CommandStack { get; init; }

    public TimelineEditor(CommandStack commandStack)
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
        if (Matches(ConsoleKey.LeftArrow, shift: true) || Matches(ConsoleKey.A, shift: true)) return AdjustDuration(-Timeline.BeatsPerColumn);
        if (Matches(ConsoleKey.RightArrow, shift: true) || Matches(ConsoleKey.D, shift: true)) return AdjustDuration(Timeline.BeatsPerColumn);

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
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(Timeline.Notes);
        Timeline.RefreshVisibleSet();
        Timeline.StatusChanged.Fire("All notes selected".ToWhite());
        return true;
    }
    private bool DeselectAll()
    {
        Timeline.SelectedNotes.Clear();
        Timeline.RefreshVisibleSet();
        Timeline.StatusChanged.Fire("Deselected all notes".ToWhite());
        return true;
    }
    private bool SelectAllLeftOrRight(ConsoleKeyInfo k)
    {
        var left = k.Key == ConsoleKey.LeftArrow;
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(Timeline.Notes.Where(n =>
            (left && n.StartBeat <= Timeline.CurrentBeat) ||
            (!left && n.StartBeat >= Timeline.CurrentBeat)));
        Timeline.RefreshVisibleSet();
        Timeline.StatusChanged.Fire("All notes selected".ToWhite());
        return true;
    }

    // --- CLIPBOARD ---
    private bool Copy()
    {
        clipboard.Clear();
        Timeline.StatusChanged.Fire($"Copied {Timeline.SelectedNotes.Count} notes to clipboard".ToWhite());
        clipboard.AddRange(Timeline.SelectedNotes);
        return true;
    }
    private bool Paste()
    {
        if (Timeline.Notes is not ListNoteSource) return true;
        if (clipboard.Count == 0) return true;
        double offset = Timeline.CurrentBeat - clipboard.Min(n => n.StartBeat);

        var pasted = new List<NoteExpression>();
        var addCmds = new List<ICommand>();

        foreach (var n in clipboard)
        {
            var nn = NoteExpression.Create(n.MidiNote,  Math.Max(0, n.StartBeat + offset), n.DurationBeats,  Timeline.Notes.BeatsPerMinute,  n.Velocity,  n.Instrument);
            pasted.Add(nn);
            addCmds.Add(new AddNoteCommand(Timeline, nn));
        }

        CommandStack.Execute(new MultiCommand(addCmds, "Paste Notes"));
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(pasted);
        return true;
    }

    // --- DELETE ---
    private bool DeleteSelected()
    {
        if (Timeline.Notes is not ListNoteSource) return true;
        if (Timeline.SelectedNotes.Count == 0) return true;

        var deleteCmds = Timeline.SelectedNotes
            .Select(note => new DeleteNoteCommand(Timeline, note))
            .ToList<ICommand>();

        CommandStack.Execute(new MultiCommand(deleteCmds, "Delete Selected Notes"));
        return true;
    }

    // --- MOVE ---
    private bool MoveSelection(ConsoleKeyInfo k)
    {
        if (Timeline.Notes is not ListNoteSource list) return true;
        if (Timeline.SelectedNotes.Count == 0) return true;

        double beatDelta = 0;
        int midiDelta = 0;
        if (k.Key == ConsoleKey.LeftArrow) beatDelta = -Timeline.BeatsPerColumn;
        else if (k.Key == ConsoleKey.RightArrow) beatDelta = Timeline.BeatsPerColumn;
        else if (k.Key == ConsoleKey.UpArrow) midiDelta = 1;
        else if (k.Key == ConsoleKey.DownArrow) midiDelta = -1;

        var updated = new List<NoteExpression>();
        var moveCmds = new List<ICommand>();

        foreach (var n in Timeline.SelectedNotes)
        {
            int newMidi = Math.Clamp(n.MidiNote + midiDelta, 0, 127);
            double newBeat = Math.Max(0, n.StartBeat + beatDelta);
            var nn = NoteExpression.Create(newMidi, newBeat, n.DurationBeats, Timeline.Notes.BeatsPerMinute, n.Velocity, n.Instrument);
            updated.Add(nn);
            moveCmds.Add(new ChangeNoteCommand(Timeline, n, nn));
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
        if (Timeline.SelectedNotes.Count == 0) return true;

        var updated = new List<NoteExpression>();
        var velCmds = new List<ICommand>();

        foreach (var n in Timeline.SelectedNotes)
        {
            int newVel = Math.Clamp(n.Velocity + delta, 1, 127);
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, n.DurationBeats, Timeline.Notes.BeatsPerMinute, newVel, n.Instrument);
            updated.Add(nn);
            velCmds.Add(new ChangeNoteCommand(Timeline, n, nn));
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
        if (Timeline.SelectedNotes.Count == 0) return true;

        var updated = new List<NoteExpression>();
        var durCmds = new List<ICommand>();

        foreach (var n in Timeline.SelectedNotes)
        {
            double newDuration = Math.Max(0.1, n.DurationBeats + deltaBeats); // Don't allow zero or negative duration
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, newDuration, Timeline.Notes.BeatsPerMinute, n.Velocity, n.Instrument);
            updated.Add(nn);
            durCmds.Add(new ChangeNoteCommand(Timeline, n, nn));
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
        addNotePreview = Timeline.AddPreviewControl();
        addNotePreview.Background = RGB.DarkGreen;
        addNotePreview.ZIndex = 0;
        Timeline.Viewport.SubscribeToAnyPropertyChange(addNotePreview, _ => PositionAddNotePreview(), addNotePreview);
        PositionAddNotePreview();
        Timeline.StatusChanged.Fire(ConsoleString.Parse("[White]Press [Cyan]p[White] to add a note here or press ALT + D to deselect."));
    }
    public void PositionAddNotePreview()
    {
        if (pendingAddNote == null || addNotePreview == null) return;
        var (start, duration, midi) = pendingAddNote.Value;
        int x = ConsoleMath.Round((start - Timeline.Viewport.FirstVisibleBeat) / Timeline.BeatsPerColumn) * VirtualTimelineGrid.ColWidthChars;
        int y = (Timeline.Viewport.FirstVisibleMidi + Timeline.Viewport.MidisOnScreen - 1 - midi) * VirtualTimelineGrid.RowHeightChars;
        int w = Math.Max(1, ConsoleMath.Round(duration / Timeline.BeatsPerColumn) * VirtualTimelineGrid.ColWidthChars);
        int h = VirtualTimelineGrid.RowHeightChars;
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
        var command = new AddNoteCommand(Timeline, NoteExpression.Create(midi, start, duration, Timeline.Notes.BeatsPerMinute, instrument: Timeline.Instrument));
        Timeline.Session.Commands.Execute(command);
        return true;
    }
    private bool DismissAddNotePreview()
    {
        ClearAddNotePreview();
        Timeline.RefreshVisibleSet();
        return true;
    }
}
