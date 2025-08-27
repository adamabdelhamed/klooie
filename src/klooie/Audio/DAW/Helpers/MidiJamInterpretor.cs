using System;
using System.Linq;

namespace klooie;

/// <summary>
/// Jam-mode MIDI utility: binds to a MIDI device and plays sustained notes only.
/// Does not affect transport, does not schedule, does not touch the grid.
/// The DAW provides the instrument via a delegate (e.g., "current track instrument").
/// </summary>
public sealed class MidiJamInterpretor : Recyclable
{
    private IMidiInput midiInput;
    private Dropdown midiDropdown;
    private IMidiProvider midiImpl;
    private MidiNoteOnOffDetector noteDetector;

    public IMidiInput? MidiInput => midiInput;

    private Func<InstrumentExpression> jamInstrumentProvider;
    private MidiLiveNoteEngine engine;

    private MidiJamInterpretor() { }
    private static readonly LazyPool<MidiJamInterpretor> pool = new(() => new MidiJamInterpretor());

    /// <param name="midiImpl">MIDI provider/bridge</param>
    /// <param name="jamInstrumentProvider">Delegate that returns the instrument to use for jamming (e.g., current track’s instrument). Must not return null.</param>
    public static MidiJamInterpretor Create(IMidiProvider midiImpl, Func<InstrumentExpression> jamInstrumentProvider)
    {
        var j = pool.Value.Rent();
        j.midiImpl = midiImpl ?? throw new ArgumentNullException(nameof(midiImpl));
        j.jamInstrumentProvider = jamInstrumentProvider ?? throw new ArgumentNullException(nameof(jamInstrumentProvider));

        // Factory: jam notes are disposable; startBeat=0, no scheduling, instrument provided by DAW.
        j.engine = MidiLiveNoteEngine.Create((noteNumber, velocity) =>
            NoteExpression.Create(
                midi: noteNumber,
                startBeat: 0,             // jamming is not recorded/scheduled
                durationBeats: -1,        // sustained
                bpm: 120,                 // unused for unscheduled/live notes; safe placeholder
                velocity: velocity,
                instrument: j.jamInstrumentProvider()));

        return j;
    }

    private void BindToMidiProduct()
    {
        if (midiImpl.TryConnect(midiDropdown.Value.Id, out IMidiInput input))
        {
            noteDetector?.Dispose();
            noteDetector = MidiNoteOnOffDetector.Create(input);
            noteDetector.NoteOn.Subscribe(HandleNoteOn, noteDetector);
            noteDetector.NoteOff.Subscribe(HandleNoteOff, noteDetector);
            midiInput = input;
        }
        else
        {
            midiInput = null;
        }
    }
     
    private void HandleNoteOn((int NoteNumber, int Velocity) ev)
    {
        if (ConsoleApp.Current.LayoutRoot.IsFocusStackAtMyLevel == false) return;
        
        // Jam mode: just start the sustained note (no transport, no grid)
        engine.TryStart(ev.NoteNumber, ev.Velocity, out _, out _);
    }

    private void HandleNoteOff(int noteNumber)
    {
        if (ConsoleApp.Current.LayoutRoot.IsFocusStackAtMyLevel == false) return;
        if (engine.TryStop(noteNumber, out _, out var tracker))
        {
            tracker.ReleaseNote();
        }
    }

    public Dropdown CreateMidiProductDropdown()
    {
        midiDropdown = new Dropdown(midiImpl.GetProductNames().Select(p => new DialogChoice() { DisplayText = p.ToConsoleString(), Id = p }));
        midiDropdown.ValueChanged.Sync(BindToMidiProduct, midiDropdown);
        return midiDropdown;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        midiInput = null;
        noteDetector?.Dispose();
        noteDetector = null;

        engine?.ReleaseAll();
        engine?.TryDispose();
        engine = null!;

        jamInstrumentProvider = null!;
        midiDropdown?.TryDispose();
        midiDropdown = null;
    }
}
