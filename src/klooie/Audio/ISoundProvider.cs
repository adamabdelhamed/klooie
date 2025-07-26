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

    public static void Debug(ConsoleString str) => Current?.EventLoop?.Invoke(()=> ConsoleApp.Current?.WriteLine(str));
    public static void Debug(string str) => Debug(str?.ToConsoleString() ?? "null".ToRed());
    public static void Debug(object o) => Debug(o?.ToString());

    /// <summary>
    /// Disposes a Recyclable object on the event loop thread if that's where it originated.
    /// Otherwise disposes on the current thread.
    /// </summary>
    /// <param name="r"></param>
    public static void DisposeMarshalled(Recyclable r)
    {
        if (r.ThreadId != Thread.CurrentThread.ManagedThreadId)
        {
            Current.EventLoop.Invoke(r, static r => r.TryDispose());
        }
        else
        {
            r.Dispose();
        }
    }

    public static void DisposeIfNotNullMarshalled(Recyclable? r)
    {
        if (r != null)
        {
            DisposeMarshalled(r);
        }
    }

    public static void Dispose(Recyclable r) => r.Dispose();

    public static void DisposeIfNotNull(Recyclable? r)
    {
        if (r != null)
        {
            Dispose(r);
        }
    }
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
    void Play(Song song, ILifetime? lifetime = null);
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
  

    public void Play(Song song, ILifetime? lifetime = null)
    {

    }

    public RecyclableList<IReleasableNote> PlaySustainedNote(NoteExpression note)
    {
        return RecyclableListPool<IReleasableNote>.Instance.Rent();
    }

}


