using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DeleteNoteCommand : ICommand
{
    private readonly ListNoteSource notes;
    private readonly VirtualTimelineGrid grid;
    private readonly NoteExpression note;
    private readonly List<NoteExpression> oldSelection;

    public string Description { get; }

    public DeleteNoteCommand(ListNoteSource notes, VirtualTimelineGrid grid, NoteExpression note, List<NoteExpression> selection, string desc = "Delete Note")
    {
        this.notes = notes;
        this.grid = grid;
        this.note = note;
        this.oldSelection = new List<NoteExpression>(selection);
        this.Description = desc;
    }

    public void Do()
    {
        notes.Remove(note);
        grid.SelectedNotes.Clear();
        grid.RefreshVisibleSet();
        grid.StatusChanged.Fire($"Deleted note {note.MidiNote}".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
    }

    public void Undo()
    {
        notes.Add(note);
        grid.SelectedNotes.Clear();
        grid.SelectedNotes.AddRange(oldSelection);
        grid.RefreshVisibleSet();
        grid.StatusChanged.Fire("Undo delete note".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
    }
}
