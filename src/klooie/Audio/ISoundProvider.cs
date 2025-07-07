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
    public IReleasableNote? PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null);
    public IReleasableNote? PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null);
    public void ScheduleSynthNote(int midiNote, long startSample, double durationSeconds, float velocity = 1.0f, SynthPatch patch = null);
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
    public IReleasableNote? PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null)
    {
        return null;
    }

    public IReleasableNote? PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null)
    {
        return null;
    }

    public void ScheduleSynthNote(int midiNote, long startSample, double durationSeconds, float velocity = 1.0f, SynthPatch patch = null)
    {
        // No-op implementation
    }
}




