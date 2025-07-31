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

    public static SustainedNoteInfo? CreateSustainedNote(NoteExpression note)
    {
        var patch = note.Instrument?.PatchFunc() ?? ElectricGuitar.Create();


        if (!patch.IsNotePlayable(note.MidiNote))
        {
            ConsoleApp.Current?.WriteLine(ConsoleString.Parse($"Note [Red]{note.MidiNote}[D] is not playable by the current instrument"));
            return null;
        }


        var ev = ScheduledNoteEvent.Create(note, patch, null);
        var result = RecyclableListPool<SynthVoiceProvider>.Instance.Rent(8);
        foreach (var voice in patch.SpawnVoices(MIDIInput.MidiNoteToFrequency(note.MidiNote), ev))
        {
            result.Items.Add(Create(voice));
        }
        return SustainedNoteInfo.Create(ev, result);
    }
}

public class SustainedNoteInfo : Recyclable, IReleasableNote
{
    private static LazyPool<SustainedNoteInfo> _pool = new(() => new SustainedNoteInfo());
    private int remainingVoices;
    private RecyclableList<SynthVoiceProvider> voices;
    private ScheduledNoteEvent ev;
    public List<SynthVoiceProvider>? Voices => voices?.Items;

    private SustainedNoteInfo() { }

    public static SustainedNoteInfo Create(ScheduledNoteEvent ev, RecyclableList<SynthVoiceProvider> voices)
    {
        var ret = _pool.Value.Rent();
        ret.ev = ev;
        ret.remainingVoices = voices.Count;
        ret.voices = voices;

        for(var i = 0; i < ret.remainingVoices; i++)
        {
            voices[i].OnDisposed(ret, static me =>
            {
                me.remainingVoices--;
                if (me.remainingVoices <= 0)
                {
                    me.Dispose();
                }
            });
        }

        return ret;
    }

    public void ReleaseNote()
    {
        for(var i = 0; i < remainingVoices; i++)
        {
            voices[i].ReleaseNote();
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        voices.Dispose();
        voices = null!;
        ev.Dispose();
        ev = null!;
        remainingVoices = 0;
    }
}
