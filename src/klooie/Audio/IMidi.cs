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
    // Optionally: long Timestamp { get; }
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

