using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AddInstrumentCommand : ICommand
{
    private readonly Workspace _workspace;
    private readonly InstrumentInfo _instrument;

    public AddInstrumentCommand(Workspace workspace, InstrumentInfo instrument)
    {
        _workspace = workspace;
        _instrument = instrument;
        Description = "Add Instrument";
    }

    public void Do() => _workspace.Instruments.Add(_instrument);
    public void Undo() => _workspace.Instruments.Remove(_instrument);
    public string Description { get; }
}

public class RemoveInstrumentCommand : ICommand
{
    private readonly Workspace _workspace;
    private readonly InstrumentInfo _instrument;
    private int? _index;

    public RemoveInstrumentCommand(Workspace workspace, InstrumentInfo instrument)
    {
        _workspace = workspace;
        _instrument = instrument;
        Description = "Remove Instrument";
    }

    public void Do()
    {
        _index = _workspace.Instruments.IndexOf(_instrument);
        if (_index >= 0) _workspace.Instruments.RemoveAt(_index.Value);
    }

    public void Undo()
    {
        if (_index.HasValue)
            _workspace.Instruments.Insert(_index.Value, _instrument);
    }
    public string Description { get; }
}
