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
    RecyclableList<IReleasableNote> PlaySustainedNote(NoteExpression note);
    void Play(Song song);
    public void ScheduleSynthNote(NoteExpression note);
    EventLoop EventLoop { get; }
    Event<NoteExpression> NotePlaying { get; }
}

public class NoOpSoundProvider : ISoundProvider
{
    public Event<NoteExpression> NotePlaying => Event<NoteExpression>.Create();
    public EventLoop EventLoop => ConsoleApp.Current;
    public VolumeKnob MasterVolume { get; set; }
    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null) { }
    public void Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null) { }
    public void Pause() { }
    public void Resume() { }
    public void ClearCache() { }
    public long SamplesRendered => 0;
   


    public void ScheduleSynthNote(NoteExpression note)
    {
        // No-op implementation
    }

    public void Play(Song song)
    {

    }

    public RecyclableList<IReleasableNote> PlaySustainedNote(NoteExpression note)
    {
        return RecyclableListPool<IReleasableNote>.Instance.Rent();
    }

}


