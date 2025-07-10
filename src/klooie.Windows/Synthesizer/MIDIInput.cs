using NAudio.Midi;

namespace klooie;
public class MIDIInput : Recyclable
{
    private MidiIn midiIn;

    private Event<MidiEvent>? midiFired;
    public Event<MidiEvent> EventFired => midiFired ??= Event<MidiEvent>.Create();
    private MidiInMessageEventArgs currentEventArgs;
    protected MIDIInput() { }
    private Lock lck = new();

    private static LazyPool<MIDIInput> pool = new(() => new MIDIInput());

    private ConsoleApp app;
    public static bool TryCreate(string midiInProductName, out MIDIInput input)
    {
        input = pool.Value.Rent();
        var success = input.TryConstruct(midiInProductName);
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

    private bool TryConstruct(string midiInProductName)
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
            currentEventArgs = e;
        }
        app.Invoke(this, static (me) =>
        {
            lock (me.lck)
            {
                me.EventFired.Fire(me.currentEventArgs.MidiEvent);
            }
        });
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        midiIn?.Dispose();
    }
}