using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AssignInstrumentCommand : TimelineCommand
{
    private readonly NoteExpression _original;
    private readonly NoteExpression _updated;
    private int? _index;

    public AssignInstrumentCommand(MelodyComposer timeline, NoteExpression original, NoteExpression updated) : base(timeline, "Assign Instrument")
    {
        _original = original;
        _updated = updated;
    }

    public void Do()
    {
        _index = Timeline.Notes.IndexOf(_original);
        if (_index >= 0)
        {
            Timeline.Notes[_index.Value] = _updated;
            Timeline.SelectedNotes.Remove(_original);
            Timeline.SelectedNotes.Add(_updated);
        }
        base.Do();
    }

    public void Undo()
    {
        if (_index.HasValue)
        {
            Timeline.Notes[_index.Value] = _original;
        }
        base.Undo();
    }
}
