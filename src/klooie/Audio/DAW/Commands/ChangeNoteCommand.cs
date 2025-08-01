using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class ChangeNoteCommand : TimelineCommand
{
    private readonly NoteExpression oldNote;
    private readonly NoteExpression newNote;

    public string Description { get; }

    public ChangeNoteCommand(VirtualTimelineGrid grid, NoteExpression oldNote, NoteExpression newNote) : base(grid, "Change Note")
    {
        this.oldNote = oldNote;
        this.newNote = newNote;
    }

    public override void Do()
    {
        int idx = Timeline.Notes.IndexOf(oldNote);
        if (idx >= 0)
        {
            Timeline.Notes[idx] = newNote;
            Timeline.SelectedNotes.Remove(oldNote);
            Timeline.SelectedNotes.Add(newNote);
        }

        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Do();
    }

    public override void Undo()
    {
        int idx = Timeline.Notes.IndexOf(newNote);
        if (idx >= 0)
        {
            Timeline.Notes[idx] = oldNote;
        }

        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Undo();
    }
}
