using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PowerArgs;
using System.Diagnostics;

namespace klooie;

public abstract class AudioPlaybackEngine : ISoundProvider
{
    private Event<NoteExpression> notePlaying;
    public Event<NoteExpression> NotePlaying
    {
        get
        {
            if(notePlaying == null)
            {
                notePlaying = Event<NoteExpression>.Create();
                scheduledSynthProvider.NotePlaying.Subscribe(notePlaying, (ev, note) => notePlaying.Fire(note), notePlaying);
            }
            return notePlaying;
        }
    }

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
            mixer = new MixingSampleProvider([sfxMixer, new SilenceProvider(new WaveFormat(SoundProvider.SampleRate, SoundProvider.BitsPerSample, SoundProvider.ChannelCount)), scheduledSynthProvider]) { ReadFully = true };
            outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 60);
            outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
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

    private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
#if DEBUG
            SoundProvider.Debug($"Playback stopped with exception: {e.Exception.ToString()}".ToRed());
#endif
        }
    }

    public void Play(string? soundId, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null) 
        => AddMixerInput(soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, maxDuration, false));


    public void Loop(string? soundId, ILifetime? lt = null, VolumeKnob? volumeKnob = null) 
        => AddMixerInput(soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, lt ?? Recyclable.Forever, true));

   
    public IReleasableNote? PlaySustainedNote(NoteExpression note)
    {
        return SynthVoiceProvider.PlaySustainedNote(note, MasterVolume);
    }

    public void Play(Song song, ILifetime? lifetime = null) => scheduledSynthProvider.ScheduleSong(song, lifetime);

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
public class SilenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }
    public SilenceProvider(WaveFormat format) => WaveFormat = format;
    bool once = false;
    public int Read(float[] buffer, int offset, int count)
    {
        if(once == false)
        {
            Thread.CurrentThread.Name = "AudioPlayback";
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            once = true;
        }
        Array.Clear(buffer, offset, count);
        return count;
    }
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
