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

    public void Do() => _workspace.AddInstrument(_instrument);
    public void Undo() => _workspace.RemoveInstrument(_instrument);
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
        _workspace.RemoveInstrument(_instrument);
    }

    public void Undo()
    {
        if (_index.HasValue)
            _workspace.InsertInstrument(_index.Value, _instrument);
    }
    public string Description { get; }
}
