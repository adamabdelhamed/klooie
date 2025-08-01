using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public interface IMidiEvent
{
    int NoteNumber { get; }
    int Velocity { get; }
    MidiCommand Command { get; }
}

public enum MidiCommand : byte
{
    //
    // Summary:
    //     Note Off
    NoteOff = 128,
    //
    // Summary:
    //     Note On
    NoteOn = 144,
    //
    // Summary:
    //     Key After-touch
    KeyAfterTouch = 160,
    //
    // Summary:
    //     Control change
    ControlChange = 176,
    //
    // Summary:
    //     Patch change
    PatchChange = 192,
    //
    // Summary:
    //     Channel after-touch
    ChannelAfterTouch = 208,
    //
    // Summary:
    //     Pitch wheel change
    PitchWheelChange = 224,
    //
    // Summary:
    //     Sysex message
    Sysex = 240,
    //
    // Summary:
    //     Eox (comes at end of a sysex message)
    Eox = 247,
    //
    // Summary:
    //     Timing clock (used when synchronization is required)
    TimingClock = 248,
    //
    // Summary:
    //     Start sequence
    StartSequence = 250,
    //
    // Summary:
    //     Continue sequence
    ContinueSequence = 251,
    //
    // Summary:
    //     Stop sequence
    StopSequence = 252,
    //
    // Summary:
    //     Auto-Sensing
    AutoSensing = 254,
    //
    // Summary:
    //     Meta-event
    MetaEvent = byte.MaxValue
}

public interface IMidiInput : ILifetime
{
    Event<IMidiEvent> EventFired { get; }
}

public interface IMidiProvider
{
    string[] GetProductNames();
    bool TryConnect(string productName, out IMidiInput input);
}

public class MidiNoteOnOffDetector : Recyclable
{
    private HashSet<int> noteTrackers = new();
    private IMidiInput input;

    private Event<(int NoteNumber, int Velocity)> _noteOn;
    private Event<int> _noteOff;

    public Event<(int NoteNumber, int Velocity)> NoteOn => _noteOn ??= Event<(int NoteNumber, int Velocity)>.Create();
    public Event<int> NoteOff => _noteOff ??= Event<int>.Create();

    private MidiNoteOnOffDetector() { }
    private static LazyPool<MidiNoteOnOffDetector> lt = new(() => new MidiNoteOnOffDetector());

    public static MidiNoteOnOffDetector Create(IMidiInput input)
    {
        var detector = lt.Value.Rent();
        detector.input = input ?? throw new ArgumentNullException(nameof(input), "MIDI input cannot be null.");
        input.EventFired.Subscribe(detector.HandleMidiEvent, detector);
        return detector;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _noteOn?.Dispose();
        _noteOn = null!;
        _noteOff?.Dispose();
        _noteOff = null!;
        input = null!;
        noteTrackers.Clear();
    }

    private void HandleMidiEvent(IMidiEvent ev)
    {
        if (IsNoteOn(ev) && noteTrackers.Contains(ev.NoteNumber) == false)
        {
            noteTrackers.Add(ev.NoteNumber);
            NoteOn.Fire((ev.NoteNumber, ev.Velocity));
        }
        else if (IsNoteOff(ev) && noteTrackers.Contains(ev.NoteNumber))
        {
            noteTrackers.Remove(ev.NoteNumber);
            NoteOff.Fire(ev.NoteNumber);
        }
    }

    public static bool IsNoteOff(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOff || (midiEvent.Command == MidiCommand.NoteOn && midiEvent.Velocity == 0);
    public static bool IsNoteOn(IMidiEvent midiEvent) => midiEvent.Command == MidiCommand.NoteOn && midiEvent.Velocity > 0;
}

public class SustainedNoteTracker : Recyclable
{
    private SustainedNoteTracker() { }
    private static LazyPool<SustainedNoteTracker> _pool = new(() => new SustainedNoteTracker());


    public NoteExpression Note { get; private set; }
    public IReleasableNote Releasable { get; private set; }
    NoteExpression NoteExpression { get; set; }
    public static SustainedNoteTracker Create(NoteExpression note, IReleasableNote releasable)
    {
        var tracker = _pool.Value.Rent();
        tracker.Note = note;
        tracker.Releasable = releasable;
        return tracker;
    }
    public void ReleaseNote()
    {
        Releasable.ReleaseNote();
        Dispose();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Note = null!;
        Releasable = null!;
    }
}