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

    public ChangeNoteCommand(MelodyComposer grid, NoteExpression oldNote, NoteExpression newNote) : base(grid, "Change Note")
    {
        this.oldNote = oldNote;
        this.newNote = newNote;
    }

    public override void Do()
    {
        int idx = Timeline.Values.IndexOf(oldNote);
        if (idx >= 0)
        {
            Timeline.Values[idx] = newNote;
            Timeline.SelectedValues.Remove(oldNote);
            Timeline.SelectedValues.Add(newNote);
        }

        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Do();
    }

    public override void Undo()
    {
        int idx = Timeline.Values.IndexOf(newNote);
        if (idx >= 0)
        {
            Timeline.Values[idx] = oldNote;
        }

        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Undo();
    }
}
