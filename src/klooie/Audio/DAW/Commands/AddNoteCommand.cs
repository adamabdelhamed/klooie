using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AddNoteCommand : TimelineCommand
{
    private readonly NoteExpression note;

    public AddNoteCommand(MidiGrid timeline, NoteExpression note) : base(timeline, "Add Note") => this.note = note;
    
    public override void Do()
    {
        Timeline.Values.Add(note);
        Timeline.Editor.ClearAddPreview();
        Timeline.StatusChanged.Fire($"Added note {note.MidiNote}".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Do();
    }

    public override void Undo()
    {
        Timeline.Values.Remove(note);
        Timeline.StatusChanged.Fire("Undo add note".ToWhite());
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        base.Undo();
    }
}