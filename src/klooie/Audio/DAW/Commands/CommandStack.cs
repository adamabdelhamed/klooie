using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

using System.Collections.Generic;

public class CommandStack
{
    private readonly List<ICommand> _history = new();
    private int _pointer = 0; // Points to first command after last committed

    public bool CanUndo => _pointer > 0;
    public bool CanRedo => _pointer < _history.Count;

    public void Execute(ICommand cmd)
    {
        // If not at the end, truncate future commands (classic undo/redo model)
        if (_pointer < _history.Count)
            _history.RemoveRange(_pointer, _history.Count - _pointer);

        cmd.Do();
        _history.Add(cmd);
        _pointer++;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _history[_pointer - 1];
        cmd.Undo();
        _pointer--;
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _history[_pointer];
        cmd.Do();
        _pointer++;
    }
}
