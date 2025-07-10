using klooie.Gaming;

namespace klooie;

public interface IReleasableNote
{
    void ReleaseNote();
}

public static class SoundProvider
{
    public const int SampleRate = 44100;
    public const int ChannelCount = 2;
    public const int BitsPerSample = 16;
    public static ISoundProvider Current { get; set; }
}


public interface ISoundProvider
{
    VolumeKnob MasterVolume { get;  }
    void Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null);
    void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null);
    void Pause();
    void Resume();
    void ClearCache();
    long SamplesRendered { get; }
    RecyclableList<IReleasableNote> PlaySustainedNote(Note note);
    void Play(List<Note> notes);
    public void ScheduleSynthNote(Note note);
    EventLoop EventLoop { get; }
}

public class NoOpSoundProvider : ISoundProvider
{
    public EventLoop EventLoop => ConsoleApp.Current;
    public VolumeKnob MasterVolume { get; set; }
    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null) { }
    public void Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null) { }
    public void Pause() { }
    public void Resume() { }
    public void ClearCache() { }
    public long SamplesRendered => 0;
   


    public void ScheduleSynthNote(Note note)
    {
        // No-op implementation
    }

    public void Play(List<Note> notes)
    {

    }

    public RecyclableList<IReleasableNote> PlaySustainedNote(Note note)
    {
        return RecyclableListPool<IReleasableNote>.Instance.Rent();
    }

}

public class Note : Recyclable
{
    private Note() { }
    private static LazyPool<Note> _pool = new(() => new Note());
    public static Note Create(int midiNode, TimeSpan start, TimeSpan duration, int velocity, ISynthPatch? patch)
    {
        var note = _pool.Value.Rent();
        note.MidiNode = midiNode;
        note.Start = start;
        note.Duration = duration;
        note.Velocity = velocity;
        note.Patch = patch;
        return note;
    }

    public static Note Create(int midiNode, int velocity, ISynthPatch? patch)
    {
        var note = _pool.Value.Rent();
        note.MidiNode = midiNode;
        note.Velocity = velocity;
        note.Patch = patch;
        return note;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        MidiNode = 0;
        Start = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        Velocity = 0;
        Patch = null;
    }

    public int MidiNode { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }
    public int Velocity { get; set; }
    public ISynthPatch? Patch { get; set; }
}


