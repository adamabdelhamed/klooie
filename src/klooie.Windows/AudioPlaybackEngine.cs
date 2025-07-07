using klooie.Gaming;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace klooie;

public abstract class AudioPlaybackEngine : ISoundProvider
{
    public const int SampleRate = 44100;
    private const int ChannelCount = 2;
    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider sfxMixer;
    public readonly ScheduledSynthProvider scheduledSynthProvider;
    private readonly MixingSampleProvider mixer; // The master mixer stays
    private EventLoop eventLoop;
    private SoundCache soundCache;
    public VolumeKnob MasterVolume { get; set; }
    public EventLoop EventLoop => eventLoop;

    public long SamplesRendered => scheduledSynthProvider.SamplesRendered;

    public AudioPlaybackEngine()
    {
        try
        {
            eventLoop = ConsoleApp.Current;
            SoundProvider.Current= this;
            if (eventLoop == null) throw new InvalidOperationException("AudioPlaybackEngine requires an event loop to be set. Please set EventLoop.Current before creating an instance of AudioPlaybackEngine.");
            var sw = Stopwatch.StartNew();
            MasterVolume = VolumeKnob.Create();
            sfxMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)) { ReadFully = true };
            scheduledSynthProvider = new ScheduledSynthProvider(); // We'll define this class next
            mixer = new MixingSampleProvider(new ISampleProvider[] { sfxMixer, scheduledSynthProvider }) { ReadFully = true };

            outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 300);
            outputDevice.Init(mixer);
            outputDevice.Play();
            soundCache = new SoundCache(LoadSounds());
            sw.Stop();
            LogSoundLoaded(sw.ElapsedMilliseconds);  
        }
        catch (Exception ex)
        {
            OnSoundFailedToLoad(ex);
        }
    }

    public void Play(string? soundId, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null) 
        => AddMixerInput(soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, maxDuration, false));


    public void Loop(string? soundId, ILifetime? lt = null, VolumeKnob? volumeKnob = null) 
        => AddMixerInput(soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, lt ?? Recyclable.Forever, true));

    public IReleasableNote PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null)
    {
        patch.Velocity = knob?.Volume ?? 1f;
        var source = SynthSignalSource.Create(frequencyHz, patch, MasterVolume, knob);
        var voice = SynthVoiceProvider.Create(source);
        mixer.AddMixerInput(voice);
        var scheduler = Game.Current?.PausableScheduler ?? ConsoleApp.Current.Scheduler;
        scheduler.Delay(durationSeconds * 1000, voice.ReleaseNote);
        return voice;
    }

    public IReleasableNote PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null)
    {
        patch.Velocity = knob?.Volume ?? 1f;
        var source = SynthSignalSource.Create(frequencyHz, patch, MasterVolume, knob);
        var voice = SynthVoiceProvider.Create(source);
        mixer.AddMixerInput(voice);
        return voice;
    }

    public void ScheduleSynthNote(
    int midiNote,
    long startSample,
    double durationSeconds,
    float velocity = 1.0f,
    SynthPatch patch = null)
    {
        float freq = MIDIInput.MidiNoteToFrequency(midiNote);
        var knob = VolumeKnob.Create();
        knob.Volume = velocity;
        var p = patch ?? SynthPatches.CreateBass();
        var source = SynthSignalSource.Create(freq, p, MasterVolume, knob);
        scheduledSynthProvider.ScheduleNote(ScheduledNoteEvent.Create(startSample, durationSeconds,  source));
    }
    private void AddMixerInput(RecyclableSampleProvider? sample)
    {
        if (sample == null) return;

        sfxMixer?.AddMixerInput(sample);
    }

    public void Pause() => outputDevice?.Pause(); 
    public void Resume() => outputDevice?.Play();

    public void ClearCache() => soundCache.Clear();

    /// <summary>
    /// Derived classes should return a dictionary where the keys are the names
    /// of the sound effects and the values are the bytes of WAV files.
    /// </summary>
    /// <returns></returns>
    protected abstract Dictionary<string, Func<Stream>> LoadSounds();

    /// <summary>
    /// By defaults this class does not throw when sounds fail to load.
    /// You can override this method to handle exceptions.
    /// </summary>
    /// <param name="ex">the exception to handle</param>
    protected virtual void OnSoundFailedToLoad(Exception ex) { }

    /// <summary>
    /// Lets you log the duration of the sound loading if you wish
    /// </summary>
    /// <param name="elapsedMilliseconds"></param>
    protected virtual void LogSoundLoaded(long elapsedMilliseconds) { }
}



public class ScheduledSynthProvider : ScheduledSignalSourceMixer, ISampleProvider
{
    private readonly WaveFormat waveFormat;

    public ScheduledSynthProvider() 
    {
        waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount);
    }

    public WaveFormat WaveFormat => waveFormat;
}
