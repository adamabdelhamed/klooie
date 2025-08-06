using klooie;

public class MelodyComposer : ProtectedConsolePanel
{
    private GridLayout layout;
    public PianoPanel Piano { get; private init; }
    public MidiGrid Grid { get; private init; }

    public StatusBar StatusBar { get; private init; }
    public BeatGridPlayer<NoteExpression> Player => Grid.Player;

    public MelodyComposer(WorkspaceSession session, ListNoteSource notes, IMidiProvider midiProvider)
    {
        var rowSpecPrefix =  "1p;1r";
        var rowOffset =  1;
        layout = ProtectedPanel.Add(new GridLayout($"{rowSpecPrefix};{StatusBar.Height}p", $"{PianoPanel.KeyWidth}p;1r")).Fill();
        Grid = layout.Add(new MidiGrid(session, notes), 1, rowOffset); // col then row here - I know its strange
        Piano = layout.Add(new PianoPanel(Grid.Viewport), 0, rowOffset);
        StatusBar = layout.Add(new StatusBar(), column: 0, row: rowOffset + 1, columnSpan: 2);

        var commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Orientation = Orientation.Horizontal };
        layout.Add(commandBar, 0, 0, columnSpan: 2);

        var midi = DAWMidi.Create(midiProvider, this);
        commandBar.Add(midi.CreateMidiProductDropdown());
        this.OnDisposed(() => midi.Dispose());
        
        // Add instrument picker
        var instrumentPicker = InstrumentPicker.CreatePickerDropdown();
        commandBar.Add(instrumentPicker);

        instrumentPicker.ValueChanged.Subscribe(() =>
        {
            Grid.Instrument = instrumentPicker.Value.Value as InstrumentExpression;
            notes.ForEach(n => n.Instrument = Grid.Instrument);
        }, instrumentPicker);

        Grid.StatusChanged.Subscribe(message => StatusBar.Message = message, this);
    }
}
