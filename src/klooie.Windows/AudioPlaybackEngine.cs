using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace klooie;

/// <summary>
/// An implementation of ISoundProvider for Windows that uses NAudio.
/// 
/// NAudio does create a second thread so that's something to be aware of.
/// </summary>
public abstract class AudioPlaybackEngine : ISoundProvider
{
    private const int SampleRate = 44100;
    private const int ChannelCount = 2;
    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider mixer;
    private EventLoop eventLoop;

    private SoundCache soundCache;

    /// <summary>
    /// Sets the volume to apply to any sounds played moving forward
    /// </summary>
    public float NewPlaySoundVolume { get; set; } = 1;

    /// <summary>
    /// Sets the master volume (0-1)
    /// </summary>
    public float MasterVolume { get; set; } = 1;

    /// <summary>
    /// Creates an AudioPlaybackEngine
    /// </summary>
    public AudioPlaybackEngine()
    {
        try
        {
            eventLoop = ConsoleApp.Current;
            if(eventLoop == null) throw new InvalidOperationException("AudioPlaybackEngine requires an event loop to be set. Please set EventLoop.Current before creating an instance of AudioPlaybackEngine.");
            var sw = Stopwatch.StartNew();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)) { ReadFully = true };
            outputDevice = new WaveOutEvent();
            outputDevice.Init(mixer);
            outputDevice.Play();
            var sounds = LoadSounds();
            soundCache = new SoundCache(LoadSounds());
            sw.Stop();
            LogSoundLoaded(sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            OnSoundFailedToLoad(ex);
        }
    }


    /// <summary>
    /// Plays the given sound
    /// </summary>
    /// <param name="soundId">the id of the sound</param>
    public void Play(string? soundId, ILifetime? maxDuration = null)
    {
        if (soundId == null) return;
        try
        {
            if (soundCache.TryCreate(eventLoop, soundId, NewPlaySoundVolume, maxDuration, false, out RecyclableSampleProvider sampleProvider) == false) return;
            AddMixerInput(sampleProvider);
        }
        catch (Exception)
        {

        }
    }

    /// <summary>
    /// Loops the given sound for the given lifetime
    /// </summary>
    /// <param name="soundId">the sound to loop</param>
    /// <param name="lt">the loop duration</param>
    public void Loop(string? soundId, ILifetime? lt = null)
    {
        if (soundId == null) return;
        lt = lt ?? Recyclable.Forever;
        try
        {
            if (soundCache.TryCreate(eventLoop, soundId, NewPlaySoundVolume, lt, true, out RecyclableSampleProvider sampleProvider) == false) return;
            AddMixerInput(sampleProvider);
        }
        catch (Exception)
        {

        }
    }

    private ISampleProvider AddMixerInput(RecyclableSampleProvider sample)
    {
        if (MasterVolume != 1)
        {
            MasterVolume = Math.Max(0, MasterVolume);
            MasterVolume = Math.Min(100, MasterVolume);
            sample.Volume *= MasterVolume;
        }
        mixer?.AddMixerInput(sample);
        return sample;
    }

    /// <summary>
    /// Pause sound playback
    /// </summary>
    public void Pause()
    {
        try
        {
            outputDevice?.Pause();
        }
        catch (Exception)
        {

        }
    }

    /// <summary>
    /// Resume sound playback
    /// </summary>
    public void Resume()
    {
        try
        {
            if (outputDevice != null && outputDevice.PlaybackState != PlaybackState.Playing)
            {
                outputDevice.Play();
            }
        }
        catch (Exception)
        {

        }
    }

    /// <summary>
    /// Derived classes should return a dictionary where the keys are the names
    /// of the sound effects and the values are the bytes of MP3 files.
    /// </summary>
    /// <returns></returns>
    protected abstract Dictionary<string, Func<Stream>> LoadSounds();

    /// <summary>
    /// By defaults this class does not throw when sounds fail to load.
    /// You can override this method to handle exceptions.
    /// </summary>
    /// <param name="ex">the exception to handle</param>
    protected virtual void OnSoundFailedToLoad(Exception ex)
    {

    }

    /// <summary>
    /// Lets you log the duration of the sound loading if you wish
    /// </summary>
    /// <param name="elapsedMilliseconds"></param>
    protected virtual void LogSoundLoaded(long elapsedMilliseconds)
    {

    }
}

