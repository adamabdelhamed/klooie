using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MidiGridCommand : ICommand
{
    protected readonly MidiGrid Timeline;
    protected readonly IReadOnlyList<NoteExpression> OldSelection;

    public string Description { get; }

    public MidiGridCommand(MidiGrid timeline, string desc)
    {
        this.Timeline = timeline;
        this.OldSelection = new List<NoteExpression>(timeline.SelectedValues).AsReadOnly();
        this.Description = desc;
    }

    public virtual void Do()
    {
        Timeline.RefreshVisibleCells();
    }

    public virtual void Undo()
    {
        Timeline.SelectedValues.Clear();
        Timeline.SelectedValues.AddRange(OldSelection);
        Timeline.RefreshVisibleCells();
    }
}