using klooie.Gaming;

namespace klooie;

public interface IReleasableNote
{
    void ReleaseNote();
}
public interface IBinaryAssetProvider
{
    bool Contains(string assetName);
    Stream Open(string assetName);
}

public interface IBinarySoundProvider : IBinaryAssetProvider
{
}

public static class BinaryAssetProvider
{
    public static IBinaryAssetProvider Current { get; set; } = new EmptyBinaryAssetProvider();
}

public sealed class EmptyBinaryAssetProvider : IBinaryAssetProvider
{
    public bool Contains(string assetName) => false;
    public Stream Open(string assetName) => throw new FileNotFoundException($"Asset '{assetName}' not found.");
}

public interface IConsoleAudioRecordingSink
{
    void WriteAudioSamples(ReadOnlySpan<float> samples, int sampleRate, int channels, long firstSampleFrame);
}

public readonly struct AudioPlaybackPosition
{
    public readonly long PlaybackId;
    public readonly string TrackId;
    public readonly double TimeSeconds;
    public readonly bool IsMusic;

    public AudioPlaybackPosition(long playbackId, string trackId, double timeSeconds, bool isMusic)
    {
        PlaybackId = playbackId;
        TrackId = trackId;
        TimeSeconds = timeSeconds;
        IsMusic = isMusic;
    }
}

public interface IAudioPlaybackPositionProvider
{
    Event<AudioPlaybackPosition> PlaybackPositionChanged { get; }
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
    ILifetime Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null, bool isMusic = false);
    void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null, bool isMusic = false);
    void Pause();
    void Resume();
    void ClearCache();
    long SamplesRendered { get; }
    IConsoleAudioRecordingSink? AudioRecordingSink { get; set; }
    IReleasableNote? PlaySustainedNote(NoteExpression note);
    Task Play(Song song, ILifetime? lifetime = null);
    EventLoop EventLoop { get; }
    public ScheduledSignalSourceMixer ScheduledSignalMixer { get; }
    public bool FailedToInitializeOrRun { get;  }
}

public class NoOpSoundProvider : ISoundProvider
{
    private readonly ScheduledSignalSourceMixer scheduledSignalMixer = new(ScheduledSignalMixerMode.PreRenderOnly);
    public EventLoop EventLoop => ConsoleApp.Current;
    public VolumeKnob MasterVolume { get; set; } = VolumeKnob.Create();
    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null, bool isMusic = false) { }
    public ILifetime Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null, bool isMusic = false) => Lifetime.Completed;
    public void Pause() { }
    public void Resume() { }
    public void ClearCache() { }
    public long SamplesRendered => 0;
    public bool FailedToInitializeOrRun => false;
    public IConsoleAudioRecordingSink? AudioRecordingSink { get; set; }

    public ScheduledSignalSourceMixer ScheduledSignalMixer => scheduledSignalMixer;

    public Task Play(Song song, ILifetime? lifetime = null)
    {
        return Task.CompletedTask;
    }

    public IReleasableNote? PlaySustainedNote(NoteExpression note) => null;

}


