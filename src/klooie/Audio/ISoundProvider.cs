using klooie.Gaming;

namespace klooie;

public interface IReleasableNote
{
    void ReleaseNote();
}

public interface IBinarySoundProvider
{
    public Stream Load(string soundId);
}

public static class SoundProvider
{
    public const int SampleRate = 44100;
    public const int ChannelCount = 2;
    public const int BitsPerSample = 16;
    public static readonly float InverseSampleRate = 1f / SampleRate;

    public static ISoundProvider Current { get; set; }

    public static void Debug(ConsoleString str) => Current?.EventLoop?.Invoke(str, static (str)=> ConsoleApp.Current?.WriteLine(str));
    public static void Debug(string str) => Debug(str?.ToConsoleString() ?? "null".ToRed());
    public static void Debug(object o) => Debug(o?.ToString());
}


public interface ISoundProvider
{
    VolumeKnob MasterVolume { get;  }
    ILifetime Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null);
    void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null);
    void Pause();
    void Resume();
    void ClearCache();
    long SamplesRendered { get; }
    IReleasableNote? PlaySustainedNote(NoteExpression note);
    Task Play(Song song, ILifetime? lifetime = null);
    EventLoop EventLoop { get; }
    public ScheduledSignalSourceMixer ScheduledSignalMixer { get; }
}

public class NoOpSoundProvider : ISoundProvider
{
    public EventLoop EventLoop => ConsoleApp.Current;
    public VolumeKnob MasterVolume { get; set; }
    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null) { }
    public ILifetime Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null) => Lifetime.Completed;
    public void Pause() { }
    public void Resume() { }
    public void ClearCache() { }
    public long SamplesRendered => 0;

    public ScheduledSignalSourceMixer ScheduledSignalMixer => throw new NotImplementedException("NoOpSoundProvider does not support ScheduledSignalMixer.");

    public Task Play(Song song, ILifetime? lifetime = null)
    {
        return Task.CompletedTask;
    }

    public IReleasableNote? PlaySustainedNote(NoteExpression note) => null;

}


