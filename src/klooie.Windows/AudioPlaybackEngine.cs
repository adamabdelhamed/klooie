using klooie.Gaming;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace klooie;

public abstract class AudioPlaybackEngine : ISoundProvider
{
    private const int SampleRate = 44100;
    private const int ChannelCount = 2;
    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider mixer;
    private EventLoop eventLoop;
    private SoundCache soundCache;
    public VolumeKnob MasterVolume { get; set; } 

    public AudioPlaybackEngine()
    {
        try
        {
            eventLoop = ConsoleApp.Current;
            if(eventLoop == null) throw new InvalidOperationException("AudioPlaybackEngine requires an event loop to be set. Please set EventLoop.Current before creating an instance of AudioPlaybackEngine.");
            var sw = Stopwatch.StartNew();
            MasterVolume = VolumeKnob.Create();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)) { ReadFully = true };
            outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 100); // Try 10–40ms
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

    public SynthVoiceProvider PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null)
    {
        patch.Velocity = knob?.Volume ?? 1f;
        var voice = SynthVoiceProvider.Create(frequencyHz, durationSeconds, patch, MasterVolume, knob);
        mixer.AddMixerInput(voice);
        var scheduler = Game.Current?.PausableScheduler ?? ConsoleApp.Current.Scheduler;
        scheduler.Delay(durationSeconds * 1000, voice.ReleaseNote);
        return voice;
    }

    public SynthVoiceProvider PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null)
    {
        patch.Velocity = knob?.Volume ?? 1f;
        var voice = SynthVoiceProvider.Create(frequencyHz, durationSeconds: double.MaxValue, patch, MasterVolume, knob);
        mixer.AddMixerInput(voice);
        return voice;
    }

    private void AddMixerInput(RecyclableSampleProvider? sample)
    {
        if (sample == null) return;

        mixer?.AddMixerInput(sample);
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

