using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DeleteNoteCommand : TimelineCommand
{

    private readonly NoteExpression note;
    public string Description { get; }

    public DeleteNoteCommand(MelodyComposer grid, NoteExpression note) : base(grid, "Delete Note")
    {
        this.note = note;
    }

    public override void Do()
    {
        Timeline.Values.Remove(note);
        Timeline.SelectedValues.Remove(note);
        Timeline.StatusChanged.Fire($"Deleted note {note.MidiNote}".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Do();
    }

    public override void Undo()
    {
        Timeline.Values.Add(note);
        Timeline.RefreshVisibleCells();
        Timeline.StatusChanged.Fire("Undo delete note".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Undo();
    }
}
