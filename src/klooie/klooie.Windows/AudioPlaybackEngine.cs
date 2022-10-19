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
    private Dictionary<string, byte[]> cachedSounds;
    private List<Lifetime> currentSoundLifetimes;
    private Dictionary<ISampleProvider, LoopInfo> runningLoops;

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
            cachedSounds = new Dictionary<string, byte[]>();
            currentSoundLifetimes = new List<Lifetime>();
            runningLoops = new Dictionary<ISampleProvider, LoopInfo>();
            var sw = Stopwatch.StartNew();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)) { ReadFully = true };
            mixer.MixerInputEnded += Mixer_MixerInputEnded;
            outputDevice = new WaveOutEvent();
            outputDevice.Init(mixer);
            outputDevice.Play();
            cachedSounds = LoadSounds();
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
    public void Play(string soundId)
    {
        try
        {
            var slowId = soundId + "slow";

            if (cachedSounds.TryGetValue(soundId, out byte[] bytes))
            {
                var sample = CreateNewSample(bytes);

                if (NewPlaySoundVolume != 1)
                {
                    sample = new VolumeSampleProvider(sample) { Volume = NewPlaySoundVolume };
                }
                AddMixerInput(sample);
            }
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
    public void Loop(string soundId, ILifetimeManager lt)
    {
        try
        {
            if (cachedSounds.TryGetValue(soundId, out byte[] bytes))
            {
                var overrideLifetime = new Lifetime();
                lock (currentSoundLifetimes)
                {
                    currentSoundLifetimes.Add(overrideLifetime);
                }

                var rawSample = CreateNewSample(bytes);

                if (NewPlaySoundVolume != 1)
                {
                    rawSample = new VolumeSampleProvider(rawSample) { Volume = NewPlaySoundVolume };
                }
                var sample = AddMixerInput(new LifetimeAwareSampleProvider(rawSample, lt));
                runningLoops.Add(sample, new LoopInfo() { Lifetime = lt, Sound = soundId });
                lt.OnDisposed(() => runningLoops.Remove(sample));
            }
        }
        catch (Exception)
        {

        }
    }

    private ISampleProvider AddMixerInput(ISampleProvider sample)
    {
        if (MasterVolume != 1)
        {
            MasterVolume = Math.Max(0, MasterVolume);
            MasterVolume = Math.Min(100, MasterVolume);
            sample = new VolumeSampleProvider(sample) { Volume = MasterVolume };
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
    /// Stops all sounds that are looping
    /// </summary>
    public void EndAllLoops()
    {
        try
        {
            lock (currentSoundLifetimes)
            {
                foreach (var sound in currentSoundLifetimes)
                {
                    sound.Dispose();
                }
                currentSoundLifetimes.Clear();
            }
        }
        catch (Exception)
        {

        }
    }

    private ISampleProvider CreateNewSample(byte[] bytes) => new Mp3FileReader(new MemoryStream(bytes)).ToSampleProvider();

    private void Mixer_MixerInputEnded(object sender, SampleProviderEventArgs e)
    {
        if (runningLoops.TryGetValue(e.SampleProvider, out LoopInfo info))
        {
            Loop(info.Sound, info.Lifetime);
            runningLoops.Remove(e.SampleProvider);
        }
    }

    /// <summary>
    /// Derived classes should return a dictionary where the keys are the names
    /// of the sound effects and the values are the bytes of MP3 files.
    /// </summary>
    /// <returns></returns>
    protected abstract Dictionary<string, byte[]> LoadSounds();

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

internal sealed class LifetimeAwareSampleProvider : ISampleProvider
{
    private ISampleProvider inner;
    private ILifetimeManager lt;
    public LifetimeAwareSampleProvider(ISampleProvider inner, ILifetimeManager lt)
    {
        this.inner = inner;
        this.lt = lt;
    }

    public WaveFormat WaveFormat => inner.WaveFormat;
    public int Read(float[] buffer, int offset, int count) => lt != null && lt.IsExpired ? 0 : inner.Read(buffer, offset, count);
}

internal sealed class VolumeSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;

    /// <summary>
    /// Initializes a new instance of VolumeSampleProvider
    /// </summary>
    /// <param name="source">Source Sample Provider</param>
    public VolumeSampleProvider(ISampleProvider source)
    {
        this.source = source;
        Volume = 1.0f;
    }

    /// <summary>
    /// WaveFormat
    /// </summary>
    public WaveFormat WaveFormat => source.WaveFormat;

    /// <summary>
    /// Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="sampleCount">Number of samples desired</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int sampleCount)
    {
        int samplesRead = source.Read(buffer, offset, sampleCount);
        if (Volume != 1f)
        {
            for (int n = 0; n < sampleCount; n++)
            {
                buffer[offset + n] *= Volume;
            }
        }
        return samplesRead;
    }

    /// <summary>
    /// Allows adjusting the volume, 1.0f = full volume
    /// </summary>
    public float Volume { get; set; }
}


internal sealed class LoopInfo
{
    public string Sound { get; set; }
    public ILifetimeManager Lifetime { get; set; }
}

