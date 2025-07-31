using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MoveNoteCommand : ICommand
{
    private readonly ListNoteSource notes;
    private readonly VirtualTimelineGrid grid;
    private readonly NoteExpression oldNote;
    private readonly NoteExpression newNote;
    private readonly List<NoteExpression> oldSelection;
    private readonly List<NoteExpression> newSelection;

    public string Description { get; }

    public MoveNoteCommand(ListNoteSource notes, VirtualTimelineGrid grid, NoteExpression oldNote, NoteExpression newNote, List<NoteExpression> oldSelection, List<NoteExpression> newSelection, string desc = "Move Note")
    {
        this.notes = notes;
        this.grid = grid;
        this.oldNote = oldNote;
        this.newNote = newNote;
        this.oldSelection = new List<NoteExpression>(oldSelection);
        this.newSelection = new List<NoteExpression>(newSelection);
        this.Description = desc;
    }

    public void Do()
    {
        int idx = notes.IndexOf(oldNote);
        if (idx >= 0)
            notes[idx] = newNote;

        grid.SelectedNotes.Clear();
        grid.SelectedNotes.AddRange(newSelection);
        grid.RefreshVisibleSet();
        grid.StatusChanged.Fire($"Moved note {oldNote.MidiNote} to {newNote.MidiNote}".ToWhite());
    }

    public void Undo()
    {
        int idx = notes.IndexOf(newNote);
        if (idx >= 0)
            notes[idx] = oldNote;

        grid.SelectedNotes.Clear();
        grid.SelectedNotes.AddRange(oldSelection);
        grid.RefreshVisibleSet();
        grid.StatusChanged.Fire($"Undo move note".ToWhite());
    }
}

