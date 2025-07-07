using NAudio.Midi;

namespace klooie;
public class MIDIInput : Recyclable
{
    private MidiIn midiIn;

    private Event<MidiEvent>? midiFired;
    public Event<MidiEvent> EventFired => midiFired ??= Event<MidiEvent>.Create();

    protected MIDIInput() { }

    private static LazyPool<MIDIInput> pool = new(() => new MIDIInput());

    public static MIDIInput Create(string midiInProductName)
    {
        var ret = pool.Value.Rent();
        ret.Construct(midiInProductName);
        return ret;
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

    private void Construct(string midiInProductName)
    {
        for (var i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var deviceInfo = MidiIn.DeviceInfo(i);
            if (deviceInfo.ProductName == midiInProductName)
            {
                midiIn = new MidiIn(i);
                midiIn.MessageReceived += OnMidiMessageReceived;
                midiIn.Start();
            }
        }

        if (midiIn == null) throw new IOException($"Did not find midi input '{midiInProductName}'");
    }

    private void OnMidiMessageReceived(object sender, MidiInMessageEventArgs e) => EventFired.Fire(e.MidiEvent);

    protected override void OnReturn()
    {
        base.OnReturn();
        midiIn.Dispose();
    }
}