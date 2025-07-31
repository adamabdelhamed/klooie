using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class TimelineEditor
{
    public required VirtualTimelineGrid Timeline { get; init; }
    private readonly List<NoteExpression> clipboard = new();

    private CommandStack CommandStack { get; init; }

    public TimelineEditor(CommandStack commandStack)
    {
        this.CommandStack = commandStack;
    }

    public bool HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.A && k.Modifiers == ConsoleModifiers.Control)
        {
            SelectAll();
            return true;
        }
        if (k.Key == ConsoleKey.D && k.Modifiers == ConsoleModifiers.Control)
        {
            DeselectAll();
            return true;
        }
        if (k.Key == ConsoleKey.Delete)
        {
            DeleteSelected();
            return true;
        }
        if (k.Key == ConsoleKey.C && k.Modifiers == ConsoleModifiers.Shift)
        {
            Copy();
            return true;
        }
        if (k.Key == ConsoleKey.V && k.Modifiers == ConsoleModifiers.Shift)
        {
            Paste();
            return true;
        }
        if (k.Modifiers == ConsoleModifiers.Control && IsArrowKey(k))
        {
            MoveSelection(k);
            return true;
        }
        if (k.Modifiers == ConsoleModifiers.Shift && (
            (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.DownArrow) ||
            k.Key == ConsoleKey.W || k.Key == ConsoleKey.S))
        {
            var isUp = k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W;
            AdjustVelocity(isUp ? 1 : -1);
            return true;
        }
        else if (k.Modifiers == ConsoleModifiers.Control && k.Key == ConsoleKey.Z)
        {
            CommandStack.Undo();
            return true;
        }
        else if (k.Modifiers == ConsoleModifiers.Control && k.Key == ConsoleKey.Y)
        {
            CommandStack.Redo();
            return true;
        }
        return false;
    }

    private void SelectAll()
    {
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(Timeline.NoteSource);
        Timeline.RefreshVisibleSet();
        Timeline.StatusChanged.Fire("All notes selected".ToWhite());
    }

    private void DeselectAll()
    {
        Timeline.SelectedNotes.Clear();
        Timeline.RefreshVisibleSet();
        Timeline.StatusChanged.Fire("Deselected all notes".ToWhite());
    }

    private void DeleteSelected()
    {
        if (Timeline.NoteSource is not ListNoteSource list) return;
        if (Timeline.SelectedNotes.Count == 0) return;

        var oldSelection = Timeline.SelectedNotes.ToList();
        var deleteCmds = Timeline.SelectedNotes
            .Select(note => new DeleteNoteCommand(list, Timeline, note, oldSelection))
            .ToList<ICommand>();

        CommandStack.Execute(new MultiCommand(deleteCmds, "Delete Selected Notes"));
    }

    private void Copy()
    {
        clipboard.Clear();
        Timeline.StatusChanged.Fire($"Copied {Timeline.SelectedNotes.Count} notes to clipboard".ToWhite());
        clipboard.AddRange(Timeline.SelectedNotes);
    }

    private void Paste()
    {
        if (Timeline.NoteSource is not ListNoteSource list) return;
        if (clipboard.Count == 0) return;
        double offset = Timeline.CurrentBeat - clipboard.Min(n => n.StartBeat);

        var pasted = new List<NoteExpression>();
        var oldSelection = Timeline.SelectedNotes.ToList();
        var addCmds = new List<ICommand>();

        foreach (var n in clipboard)
        {
            var nn = NoteExpression.Create(
                n.MidiNote,
                Math.Max(0, n.StartBeat + offset),
                n.DurationBeats,
                n.Velocity,
                n.Instrument);
            pasted.Add(nn);
            addCmds.Add(new AddNoteCommand(list, Timeline, nn, oldSelection));
        }

        // After paste, only the new notes are selected
        CommandStack.Execute(new MultiCommand(addCmds, "Paste Notes"));
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(pasted);
    }

    private void MoveSelection(ConsoleKeyInfo k)
    {
        if (Timeline.NoteSource is not ListNoteSource list) return;
        if (Timeline.SelectedNotes.Count == 0) return;

        double beatDelta = 0;
        int midiDelta = 0;
        if (k.Key == ConsoleKey.LeftArrow) beatDelta = -Timeline.BeatsPerColumn;
        else if (k.Key == ConsoleKey.RightArrow) beatDelta = Timeline.BeatsPerColumn;
        else if (k.Key == ConsoleKey.UpArrow) midiDelta = 1;
        else if (k.Key == ConsoleKey.DownArrow) midiDelta = -1;

        var oldSelection = Timeline.SelectedNotes.ToList();
        var updated = new List<NoteExpression>();
        var moveCmds = new List<ICommand>();

        foreach (var n in oldSelection)
        {
            int idx = list.IndexOf(n);
            if (idx < 0) continue;
            int newMidi = Math.Clamp(n.MidiNote + midiDelta, 0, 127);
            double newBeat = Math.Max(0, n.StartBeat + beatDelta);
            var nn = NoteExpression.Create(newMidi, newBeat, n.DurationBeats, n.Velocity, n.Instrument);
            updated.Add(nn);
            moveCmds.Add(
                new MoveNoteCommand(list, Timeline, n, nn, oldSelection, updated)
            );
        }

        if (moveCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(moveCmds, "Move Notes"));
        }
    }

    private void AdjustVelocity(int delta)
    {
        if (Timeline.NoteSource is not ListNoteSource list) return;
        if (Timeline.SelectedNotes.Count == 0) return;

        var oldSelection = Timeline.SelectedNotes.ToList();
        var updated = new List<NoteExpression>();
        var velCmds = new List<ICommand>();
        var isSingleNote = Timeline.SelectedNotes.Count == 1;

        foreach (var n in oldSelection)
        {
            int idx = list.IndexOf(n);
            if (idx < 0) continue;
            int newVel = Math.Clamp(n.Velocity + delta, 1, 127);
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, n.DurationBeats, newVel, n.Instrument);
            updated.Add(nn);
            velCmds.Add(
                new ChangeVelocityCommand(list, Timeline, n, nn, oldSelection, updated)
            );
        }

        if (velCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(velCmds, "Change Velocity"));
        }
    }

    private static bool IsArrowKey(ConsoleKeyInfo k) =>
        k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.RightArrow ||
        k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.DownArrow;
}
