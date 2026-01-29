using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace klooie;

public class AudioPlaybackEngine : ISoundProvider
{
    public const int SampleRate = SoundProvider.SampleRate;
    private const int ChannelCount = SoundProvider.ChannelCount;
    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider sfxMixer;
    public readonly ScheduledSynthProvider scheduledSynthProvider;
    private readonly MixingSampleProvider mixer; // The master mixer stays
    private EventLoop eventLoop;
    private SoundCache soundCache;
    public VolumeKnob MasterVolume { get; set; }
    public EventLoop EventLoop => eventLoop;
    public ScheduledSignalSourceMixer ScheduledSignalMixer => scheduledSynthProvider;
    public long SamplesRendered => scheduledSynthProvider.SamplesRendered;
    public bool FailedToInitializeOrRun { get; private set; }

    private ReadWatchdogSampleProvider? readWatchdog;
    private int audioFailed; // 0/1
    private const int StallMs = 500;
    public AudioPlaybackEngine(IBinarySoundProvider provider = null)
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
            mixer = new MixingSampleProvider([sfxMixer, scheduledSynthProvider]) { ReadFully = true };
            readWatchdog = new ReadWatchdogSampleProvider(mixer);
            outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 60);
            outputDevice.PlaybackStopped += (_, __) => eventLoop.Invoke(()=> FailAudio());
            outputDevice.Init(readWatchdog);
            outputDevice.Play();
            StartAudioStallMonitor(); // add this call
            soundCache = new SoundCache(provider);
            sw.Stop();
            LogSoundLoaded(sw.ElapsedMilliseconds);  
        }
        catch (Exception ex)
        {
            FailAudio(ex);
        }
    }

    protected void RebindEventLoop(EventLoop loop)
    {
        eventLoop = loop;
        StartAudioStallMonitor();
    }

    private void StartAudioStallMonitor()
    {
        eventLoop.Invoke(async () =>
        {
            while (true)
            {
                await Task.Delay(250);
                if (FailedToInitializeOrRun || Volatile.Read(ref audioFailed) == 1) break;
                if (readWatchdog == null) continue;
                if (outputDevice == null) continue;
                if (outputDevice.PlaybackState != PlaybackState.Playing) continue;

                var now = Stopwatch.GetTimestamp();
                var lastRead = readWatchdog.LastReadTimestamp;
                var elapsedMs = (now - lastRead) * 1000 / Stopwatch.Frequency;
                if (elapsedMs > StallMs) FailAudio();
            }
        });
    }

    protected void FailAudio(Exception? ex = null)
    {
        if (Interlocked.Exchange(ref audioFailed, 1) == 1) return;
        if (FailedToInitializeOrRun) return;
        FailedToInitializeOrRun = true;
        try { outputDevice?.Stop(); } catch { }
        try { outputDevice?.Dispose(); } catch { }
        OnSoundFailedToLoad(ex ?? new Exception("Audio engine failed or stalled"));
    }

    public ILifetime Play(string? soundId, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (FailedToInitializeOrRun) return Lifetime.Completed;
        var input = soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, maxDuration, false, isMusic);
        if(input == null) return Lifetime.Completed;
        AddMixerInput(input);
        return input;
    }


    public void Loop(string? soundId, ILifetime? lt = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (FailedToInitializeOrRun) return;
        AddMixerInput(soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, lt ?? Lifetime.Forever, true, isMusic));
    }


    public IReleasableNote? PlaySustainedNote(NoteExpression note)
    {
        if (FailedToInitializeOrRun) return null;
        var ret = SynthVoiceProvider.CreateSustainedNote(note);
        if (ret.Voices != null)
        {
            for (var i = 0; i < ret.Voices.Count; i++)
            {
                var voice = ret.Voices[i];
                mixer.AddMixerInput(voice);
            }
        }
        return ret;
    }

    public async Task Play(Song song, ILifetime? lifetime = null)
    {
        if (FailedToInitializeOrRun)
        {
            await Task.Yield();
            return;
        }

        CancellationToken? token = null;
        if(lifetime != null)
        {
            var source = new CancellationTokenSource();
            lifetime.OnDisposed(source, static (source) => source.Cancel());
            token = source.Token;
        }
        await scheduledSynthProvider.ScheduleSong(song, token);
    }


    private void AddMixerInput(RecyclableSampleProvider? sample)
    {
        if (sample == null) return;
        sfxMixer?.AddMixerInput(sample);
    }

    public void Pause()
    {
        if (FailedToInitializeOrRun) return;
        if(outputDevice == null) return;
        if(outputDevice.PlaybackState != PlaybackState.Playing) return;
        outputDevice.Pause();
    }
    public void Resume()
    {
        if (FailedToInitializeOrRun) return;
        if(outputDevice == null) return;
        if (outputDevice.PlaybackState != PlaybackState.Paused) return;
        outputDevice.Play();
    }
    public void ClearCache()
    {
        if (FailedToInitializeOrRun) return;
        soundCache.Clear();
    }


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
    public ScheduledSynthProvider() => waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount);
    public WaveFormat WaveFormat => waveFormat;
}

internal sealed class ReadWatchdogSampleProvider : ISampleProvider
{
    private readonly ISampleProvider inner;
    private long lastReadTimestamp;

    public ReadWatchdogSampleProvider(ISampleProvider inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        lastReadTimestamp = Stopwatch.GetTimestamp();
    }

    public WaveFormat WaveFormat => inner.WaveFormat;
    public long LastReadTimestamp => Interlocked.Read(ref lastReadTimestamp);

    public int Read(float[] buffer, int offset, int count)
    {
        Interlocked.Exchange(ref lastReadTimestamp, Stopwatch.GetTimestamp());
        return inner.Read(buffer, offset, count);
    }
}
