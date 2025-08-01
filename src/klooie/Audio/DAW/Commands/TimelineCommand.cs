using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class TimelineCommand : ICommand
{
    protected readonly VirtualTimelineGrid Timeline;
    protected readonly IReadOnlyList<NoteExpression> OldSelection;

    public string Description { get; }

    public TimelineCommand(VirtualTimelineGrid timeline, string desc)
    {
        this.Timeline = timeline;
        this.OldSelection = new List<NoteExpression>(timeline.SelectedNotes).AsReadOnly();
        this.Description = desc;
    }

    public virtual void Do()
    {
        Timeline.RefreshVisibleSet();
    }

    public virtual void Undo()
    {
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(OldSelection);
        Timeline.RefreshVisibleSet();
    }
}