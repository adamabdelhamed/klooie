using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AssignInstrumentCommand : ICommand
{
    private readonly ListNoteSource _notes;
    private readonly NoteExpression _original;
    private readonly NoteExpression _updated;
    private int? _index;

    public AssignInstrumentCommand(ListNoteSource notes, NoteExpression original, NoteExpression updated, string description = "Assign Instrument")
    {
        _notes = notes;
        _original = original;
        _updated = updated;
        Description = description;
    }

    public void Do()
    {
        _index = _notes.IndexOf(_original);
        if (_index >= 0)
            _notes[_index.Value] = _updated;
    }

    public void Undo()
    {
        if (_index.HasValue)
            _notes[_index.Value] = _original;
    }

    public string Description { get; }
}
