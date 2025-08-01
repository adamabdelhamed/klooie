using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class ChangeVelocityCommand : TimelineCommand
{
    private readonly NoteExpression oldNote;
    private readonly NoteExpression newNote;

    public string Description { get; }

    public ChangeVelocityCommand(VirtualTimelineGrid grid, NoteExpression oldNote, NoteExpression newNote) : base(grid, "Change Velocity")
    {
        this.oldNote = oldNote;
        this.newNote = newNote;
    }

    public override void Do()
    {
        int idx = Timeline.Notes.IndexOf(oldNote);
        if (idx >= 0) Timeline.Notes[idx] = newNote;

        Timeline.StatusChanged.Fire($"Changed velocity of note {newNote.MidiNote} to {newNote.Velocity}".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Do();
    }

    public override void Undo()
    {
        int idx = Timeline.Notes.IndexOf(newNote);
        if (idx >= 0) Timeline.Notes[idx] = oldNote;

        Timeline.StatusChanged.Fire("Undo velocity change".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Undo();
    }
}
