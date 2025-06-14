﻿using NAudio.Wave;
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
    private List<Recyclable> currentSoundLifetimes;
    private HashSet<string> soundIds;
    private Dictionary<ISampleProvider, LoopInfo> runningLoops;
    private SampleFactory samplePool;

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
            currentSoundLifetimes = new List<Recyclable>();
            runningLoops = new Dictionary<ISampleProvider, LoopInfo>();
            var sw = Stopwatch.StartNew();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)) { ReadFully = true };
            mixer.MixerInputEnded += Mixer_MixerInputEnded;
            outputDevice = new WaveOutEvent();
            outputDevice.Init(mixer);
            outputDevice.Play();
            var sounds = LoadSounds();
            soundIds = new HashSet<string>(sounds.Keys, StringComparer.OrdinalIgnoreCase);
            samplePool = new SampleFactory(LoadSounds());
            sw.Stop();
            LogSoundLoaded(sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            OnSoundFailedToLoad(ex);
        }
    }

    protected virtual string ReplaceSoundHook(string aboutToPlay) => aboutToPlay;

    /// <summary>
    /// Plays the given sound
    /// </summary>
    /// <param name="soundId">the id of the sound</param>
    public void Play(string? soundId, ILifetime? maxDuration = null)
    {
        if (soundId == null) return;
        try
        {
            var overrideSound = ReplaceSoundHook(soundId);
            var toPlay = soundIds.Contains(overrideSound) ? overrideSound : soundId;
            if (soundIds.Contains(toPlay))
            {
                var sample = CreateNewSample(toPlay);

                if (NewPlaySoundVolume != 1)
                {
                    sample = new VolumeSampleProvider(sample) { Volume = NewPlaySoundVolume };
                }

                if (maxDuration != null)
                {
                    sample = AddMixerInput(new LifetimeAwareSampleProvider(sample, maxDuration));
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
    public void Loop(string? soundId, ILifetime? lt = null)
    {
        if (soundId == null) return;
        lt = lt ?? Recyclable.Forever;
        try
        {
            if (soundIds.Contains(soundId))
            {
                var overrideLifetime = DefaultRecyclablePool.Instance.Rent();
                lock (currentSoundLifetimes)
                {
                    currentSoundLifetimes.Add(overrideLifetime);
                }

                var rawSample = CreateNewSample(soundId);

                if (NewPlaySoundVolume != 1)
                {
                    rawSample = new VolumeSampleProvider(rawSample) { Volume = NewPlaySoundVolume };
                }
                var sample = AddMixerInput(new LifetimeAwareSampleProvider(rawSample, lt));
                runningLoops.Add(sample, new LoopInfo() { Lifetime = lt, Sound = soundId });
                lt.OnDisposed(sample, (sampleObj) =>
                {
                    runningLoops.Remove((ISampleProvider)sampleObj);
                });
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

    private ISampleProvider CreateNewSample(string soundId) => samplePool.Create(soundId);

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

    private sealed class SampleFactory
    {
        private sealed class SoundContext
        {
            public required Func<Stream> StreamFactory { get; init; }
        }

        private Dictionary<string, SoundContext> soundContext;

        // Map from sample provider back to its soundId
        //private Dictionary<ISampleProvider, string> soundIdLookup;

        public SampleFactory(Dictionary<string, Func<Stream>> rawSoundData)
        {
            // Create a single memory copy of all sounds
            soundContext = new Dictionary<string, SoundContext>(StringComparer.OrdinalIgnoreCase);
            //soundIdLookup = new Dictionary<ISampleProvider, string>();

            foreach (var kvp in rawSoundData)
            {
                soundContext[kvp.Key] = new SoundContext()
                {
                    StreamFactory = kvp.Value,
                };
            }
        }

        public ISampleProvider Create(string soundId)
        {
            var context = soundContext[soundId];
            return CreateFreshSample(soundId, context);
        }

 

      
        private ISampleProvider CreateFreshSample(string soundId, SoundContext context)
        {
            var freshCopy = context.StreamFactory();
            var waveReader = new WaveFileReader(freshCopy);
            var freshSample = waveReader.ToSampleProvider();
            //soundIdLookup[freshSample] = soundId;
            return freshSample;
        }
        /*
        private ISampleProvider UnwrapSampleProvider(ISampleProvider sample)
        {
            while (!soundIdLookup.ContainsKey(sample))
            {
                sample = sample is LifetimeAwareSampleProvider lifetimeAware ? lifetimeAware.InnerSample :
                         sample is VolumeSampleProvider volumeProvider ? volumeProvider.InnerSample :
                         throw new InvalidOperationException("Sample provider type not recognized in Return()");
            }

            return sample;
        }
        */
    }
}

internal sealed class LifetimeAwareSampleProvider : ISampleProvider
{
    internal ISampleProvider InnerSample;
    private ILifetime lt;
    private int lease;
    public LifetimeAwareSampleProvider(ISampleProvider inner, ILifetime lt)
    {
        this.InnerSample = inner;
        this.lt = lt;
        this.lease = lt.Lease;
        lt.OnDisposed(() =>
        {
            lt = null;
            lease = 0;
            InnerSample = null;
        });
    }

    public WaveFormat WaveFormat => InnerSample.WaveFormat;
    public int Read(float[] buffer, int offset, int count) => lt?.IsStillValid(lease) == true ? InnerSample.Read(buffer, offset, count) : 0;
}

internal sealed class VolumeSampleProvider : ISampleProvider
{
    internal ISampleProvider InnerSample;

    /// <summary>
    /// Initializes a new instance of VolumeSampleProvider
    /// </summary>
    /// <param name="source">Source Sample Provider</param>
    public VolumeSampleProvider(ISampleProvider source)
    {
        this.InnerSample = source;
        Volume = 1.0f;
    }

    /// <summary>
    /// WaveFormat
    /// </summary>
    public WaveFormat WaveFormat => InnerSample.WaveFormat;

    /// <summary>
    /// Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="sampleCount">Number of samples desired</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int sampleCount)
    {
        int samplesRead = InnerSample.Read(buffer, offset, sampleCount);
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
    public ILifetime Lifetime { get; set; }
}

