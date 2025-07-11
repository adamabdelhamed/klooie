using NAudio.Midi;

namespace klooie;
public class MIDIInput : Recyclable, IMidiInput
{
    private MidiIn midiIn;

    private Event<IMidiEvent>? midiFired;
    public Event<IMidiEvent> EventFired => midiFired ??= Event<IMidiEvent>.Create();
    private Queue<MidiInMessageEventArgs> eventsToBeProcessed = new();
    protected MIDIInput() { }
    private Lock lck = new();

    private static LazyPool<MIDIInput> pool = new(() => new MIDIInput());

    private ConsoleApp app;
    public static bool TryCreate(string midiInProductName, out MIDIInput input)
    {
        input = pool.Value.Rent();
        var success = input.TryOpen(midiInProductName);
        if (success == false) input.Dispose();
        return success;
    }

    public static float MidiNoteToFrequency(int noteNumber)
    {
        return 440f * (float)Math.Pow(2, (noteNumber - 69) / 12.0);
    }

    public static bool IsNoteOff(MidiEvent midiEvent)
    {
        return midiEvent.CommandCode == MidiCommandCode.NoteOff ||
               (midiEvent is NoteOnEvent noteOn && noteOn.Velocity == 0);
    }

    private bool TryOpen(string midiInProductName)
    {
        app = ConsoleApp.Current;
        if(app == null) throw new InvalidOperationException("MIDIInput requires a ConsoleApp to be running. Please start a ConsoleApp before using MIDIInput.");
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var deviceInfo = MidiIn.DeviceInfo(i);
            if (deviceInfo.ProductName == midiInProductName)
            {
                try
                {
                    midiIn = new MidiIn(i);
                    midiIn.MessageReceived += OnMidiMessageReceived;
                    midiIn.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private void OnMidiMessageReceived(object sender, MidiInMessageEventArgs e)
    {
        lock (lck)
        {
            eventsToBeProcessed.Enqueue(e);
        }
        app.Invoke(this, static (me) => me.DrainEvents());
    }

    private void DrainEvents()
    {
        List<MidiEventWrapper> toFire = new();
        lock (lck)
        {
            while (eventsToBeProcessed.Count > 0)
            {
                toFire.Add(new MidiEventWrapper(eventsToBeProcessed.Dequeue().MidiEvent));
            }
        }
        foreach (var evt in toFire)
        {
            EventFired.Fire(evt);
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        midiIn?.Dispose();
    }
}

public class MidiEventWrapper : IMidiEvent
{
    private readonly MidiEvent midiEvent;
    public MidiEventWrapper(MidiEvent midiEvent) => this.midiEvent = midiEvent;
    public int NoteNumber => (midiEvent as NoteEvent)?.NoteNumber ?? 0;
    public int Velocity => (midiEvent as NoteEvent)?.Velocity ?? 0;
    public MidiCommand Command => (MidiCommand)midiEvent.CommandCode;
}