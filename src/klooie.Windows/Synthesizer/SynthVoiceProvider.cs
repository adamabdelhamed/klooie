using NAudio.Wave;
using PowerArgs;

namespace klooie;

public class SynthVoiceProvider : RecyclableAudioProvider, IReleasableNote
{
    private SynthSignalSource source;
    public static readonly LazyPool<SynthVoiceProvider> _pool = new(() => new SynthVoiceProvider());
    public bool IsDone => source.IsDone;
    private SynthVoiceProvider() { }

    public static SynthVoiceProvider Create(SynthSignalSource source)
    {
        var ret = _pool.Value.Rent();
        ret.source = source;
        return ret;
    }
    private WaveFormat waveFormat = new WaveFormat(SoundProvider.SampleRate, SoundProvider.BitsPerSample, SoundProvider.ChannelCount);
    public override WaveFormat WaveFormat => waveFormat;

    public void ReleaseNote() => source.ReleaseNote();

    public override int Read(float[] buffer, int offset, int count)
    {
        return source.Render(buffer, offset, count);
    }

    protected override void OnReturn()
    {
        source?.Dispose();
        source = null;
        base.OnReturn();
    }

    public static IReleasableNote? PlaySustainedNote(NoteExpression note, VolumeKnob masterVolume)
    {
        var patch = note.Instrument?.PatchFunc() ?? ElectricGuitar.Create();
        patch.WithVolume(note.Velocity / 127f);
        if (!patch.IsNotePlayable(note.MidiNote))
        {
            ConsoleApp.Current?.WriteLine(ConsoleString.Parse($"Note [Red]{note.MidiNote}[D] is not playable by the current instrument"));
            return null;
        }


        var tempEvent = ScheduledNoteEvent.Create(note, patch, null);
        var result = RecyclableListPool<SynthVoiceProvider>.Instance.Rent(8);
        foreach (var voice in patch.SpawnVoices(MIDIInput.MidiNoteToFrequency(note.MidiNote), masterVolume, tempEvent))
        {
            result.Items.Add(Create(voice));
        }
        tempEvent.Dispose();
        return VoiceCountTracker.Track(result);
    }
}

internal class VoiceCountTracker : Recyclable, IReleasableNote
{
    private static LazyPool<VoiceCountTracker> _pool = new(() => new VoiceCountTracker());
    private int remainingVoices;
    private RecyclableList<SynthVoiceProvider> voices;
    private VoiceCountTracker() { }

    public static VoiceCountTracker Track(RecyclableList<SynthVoiceProvider> voices)
    {
        var tracker = _pool.Value.Rent();
        tracker.remainingVoices = voices.Count;
        tracker.voices = voices;
        return tracker;
    }

    public void ReleaseNote()
    {
        for(var i = 0; i < remainingVoices; i++)
        {
            voices[i].ReleaseNote();
        }
        Dispose();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        voices.Dispose();
        voices = null!;
    }
}
