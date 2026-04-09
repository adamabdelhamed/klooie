using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace klooie;

public class AudioPlaybackEngine : ISoundProvider
{
    public const int SampleRate = SoundProvider.SampleRate;
    private const int ChannelCount = SoundProvider.ChannelCount;
    private const int DeviceLatencyMilliseconds = 120;
    private const int RecoveryRetryMilliseconds = 1500;
    private static readonly string PlaybackDebugLogPath = Path.Combine(Path.GetTempPath(), "ttbs-audio-debug.log");

    private readonly object outputDeviceLock = new();
    private readonly MixingSampleProvider sfxMixer;
    public readonly ScheduledSynthProvider scheduledSynthProvider;
    private readonly MixingSampleProvider masterMixer;
    private readonly ISampleProvider mixer;
    private readonly MMDeviceEnumerator deviceEnumerator;
    private readonly AudioDeviceNotificationClient notificationClient;
    private readonly SoundCache soundCache;

    private EventLoop eventLoop;
    private IWavePlayer? outputDevice;
    private int recoveryAttemptActive;
    private int recoveryScheduled;
    private bool shouldBePlaying = true;
    private bool everInitialized;

    public VolumeKnob MasterVolume { get; set; }
    public EventLoop EventLoop => eventLoop;
    public ScheduledSignalSourceMixer ScheduledSignalMixer => scheduledSynthProvider;
    public long SamplesRendered => scheduledSynthProvider.SamplesRendered;
    public bool FailedToInitializeOrRun { get; private set; }

    public AudioPlaybackEngine(IBinarySoundProvider provider = null)
    {
        try
        {
            eventLoop = ConsoleApp.Current;
            SoundProvider.Current = this;
            if (eventLoop == null) throw new InvalidOperationException("AudioPlaybackEngine requires an event loop to be set. Please set EventLoop.Current before creating an instance of AudioPlaybackEngine.");

            var sw = Stopwatch.StartNew();
            MasterVolume = VolumeKnob.Create();
            sfxMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount)) { ReadFully = true };
            scheduledSynthProvider = new ScheduledSynthProvider();
            masterMixer = new MixingSampleProvider([sfxMixer, scheduledSynthProvider]) { ReadFully = true };
            mixer = new OutputProtectionSampleProvider(masterMixer);
            soundCache = new SoundCache(provider);
            deviceEnumerator = new MMDeviceEnumerator();
            notificationClient = new AudioDeviceNotificationClient(this);

            TryRegisterForDeviceNotifications();
            RebindEventLoop(eventLoop);
            AttemptAudioRecovery("initial startup");

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
        DebugLogAudioEvent($"Rebound audio engine to event loop '{loop.GetType().Name}' on thread {loop.Thread?.ManagedThreadId}");
    }

    private void TryRegisterForDeviceNotifications()
    {
        try
        {
            deviceEnumerator.RegisterEndpointNotificationCallback(notificationClient);
        }
        catch (Exception ex)
        {
            DebugLogAudioEvent("Failed to register audio device notifications", ex);
        }
    }

    private PlaybackState CurrentPlaybackState
    {
        get
        {
            lock (outputDeviceLock)
            {
                return outputDevice?.PlaybackState ?? PlaybackState.Stopped;
            }
        }
    }

    private IWavePlayer? CurrentOutputDevice
    {
        get
        {
            lock (outputDeviceLock)
            {
                return outputDevice;
            }
        }
    }

    private void HandlePlaybackStopped(object? sender, StoppedEventArgs args)
    {
        DebugLogAudioEvent(
            $"PlaybackStopped fired. State={CurrentPlaybackState}, Failed={FailedToInitializeOrRun}, Draining={eventLoop?.IsDrainingOrDrained}",
            args.Exception);

        if (FailedToInitializeOrRun || eventLoop?.IsDrainingOrDrained == true) return;
        ScheduleRecovery(args.Exception == null ? "unexpected playback stop" : "playback stopped with exception", args.Exception);
    }

    internal void NotifyDeviceChanged(string reason)
    {
        if (FailedToInitializeOrRun || eventLoop?.IsDrainingOrDrained == true) return;
        ScheduleRecovery(reason);
    }

    private void ScheduleRecovery(string reason, Exception? ex = null)
    {
        DebugLogAudioEvent($"Scheduling audio recovery: {reason}", ex);
        if (Interlocked.Exchange(ref recoveryScheduled, 1) == 1) return;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RecoveryRetryMilliseconds);
                if (FailedToInitializeOrRun || eventLoop?.IsDrainingOrDrained == true) return;
                eventLoop.Invoke((engine: this, reason), static state => state.engine.AttemptAudioRecovery(state.reason));
            }
            finally
            {
                Interlocked.Exchange(ref recoveryScheduled, 0);
            }
        });
    }

    private void AttemptAudioRecovery(string reason)
    {
        if (FailedToInitializeOrRun || eventLoop?.IsDrainingOrDrained == true) return;
        if (Interlocked.Exchange(ref recoveryAttemptActive, 1) == 1) return;

        try
        {
            DebugLogAudioEvent($"Attempting audio recovery: {reason}");
            RecreateOutputDevice();
            everInitialized = true;
            DebugLogAudioEvent($"Audio recovery succeeded. PlaybackState={CurrentPlaybackState}");
        }
        catch (Exception ex)
        {
            DebugLogAudioEvent($"Audio recovery failed: {reason}", ex);
            DisposeOutputDevice();
            OnSoundFailedToLoad(ex);
            ScheduleRecovery($"retry after failure: {reason}", ex);
        }
        finally
        {
            Interlocked.Exchange(ref recoveryAttemptActive, 0);
        }
    }

    private void RecreateOutputDevice()
    {
        var newOutputDevice = CreateOutputDevice();
        IWavePlayer? oldOutputDevice;
        lock (outputDeviceLock)
        {
            oldOutputDevice = outputDevice;
            outputDevice = newOutputDevice;
        }

        if (ReferenceEquals(oldOutputDevice, newOutputDevice)) return;

        if (oldOutputDevice != null)
        {
            oldOutputDevice.PlaybackStopped -= HandlePlaybackStopped;
            try { oldOutputDevice.Stop(); } catch { }
            try { oldOutputDevice.Dispose(); } catch { }
        }
    }

    private IWavePlayer CreateOutputDevice()
    {
        var newOutputDevice = new WasapiOut(AudioClientShareMode.Shared, false, DeviceLatencyMilliseconds);
        newOutputDevice.PlaybackStopped += HandlePlaybackStopped;
        newOutputDevice.Init(mixer);
        if (shouldBePlaying) newOutputDevice.Play();
        return newOutputDevice;
    }

    private void DisposeOutputDevice()
    {
        IWavePlayer? oldOutputDevice;
        lock (outputDeviceLock)
        {
            oldOutputDevice = outputDevice;
            outputDevice = null;
        }

        if (oldOutputDevice == null) return;
        oldOutputDevice.PlaybackStopped -= HandlePlaybackStopped;
        try { oldOutputDevice.Stop(); } catch { }
        try { oldOutputDevice.Dispose(); } catch { }
    }

    protected void FailAudio(Exception? ex = null)
    {
        if (FailedToInitializeOrRun) return;
        FailedToInitializeOrRun = true;
        DebugLogAudioEvent("FailAudio invoked", ex);
        try { deviceEnumerator?.UnregisterEndpointNotificationCallback(notificationClient); } catch { }
        try { deviceEnumerator?.Dispose(); } catch { }
        DisposeOutputDevice();
        OnSoundFailedToLoad(ex ?? new Exception("Audio engine failed or stalled"));
    }

    public ILifetime Play(string? soundId, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (FailedToInitializeOrRun) return Lifetime.Completed;

        var input = soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, maxDuration, false, isMusic);
        if (input == null) return Lifetime.Completed;

        AddMixerInput(input);
        if (!everInitialized || CurrentOutputDevice == null) ScheduleRecovery("play requested while output device unavailable");
        return input;
    }

    public void Loop(string? soundId, ILifetime? lt = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (FailedToInitializeOrRun) return;

        var input = soundCache.GetSample(eventLoop, soundId, MasterVolume, volumeKnob, lt ?? Lifetime.Forever, true, isMusic);
        AddMixerInput(input);
        if (input != null && (!everInitialized || CurrentOutputDevice == null)) ScheduleRecovery("loop requested while output device unavailable");
    }

    public IReleasableNote? PlaySustainedNote(NoteExpression note)
    {
        if (FailedToInitializeOrRun) return null;

        var ret = SynthVoiceProvider.CreateSustainedNote(note);
        if (ret.Voices != null)
        {
            for (var i = 0; i < ret.Voices.Count; i++)
            {
                masterMixer.AddMixerInput(ret.Voices[i]);
            }
        }

        if (!everInitialized || CurrentOutputDevice == null) ScheduleRecovery("sustained note requested while output device unavailable");
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
        if (lifetime != null)
        {
            var source = new CancellationTokenSource();
            lifetime.OnDisposed(source, static source => source.Cancel());
            token = source.Token;
        }

        await scheduledSynthProvider.ScheduleSong(song, token);
        if (!everInitialized || CurrentOutputDevice == null) ScheduleRecovery("song scheduled while output device unavailable");
    }

    private void AddMixerInput(RecyclableSampleProvider? sample)
    {
        if (sample == null) return;
        sfxMixer.AddMixerInput(sample);
    }

    public void Pause()
    {
        if (FailedToInitializeOrRun) return;

        shouldBePlaying = false;
        var currentOutputDevice = CurrentOutputDevice;
        if (currentOutputDevice == null) return;
        if (currentOutputDevice.PlaybackState != PlaybackState.Playing) return;

        DebugLogAudioEvent("Pause requested");
        currentOutputDevice.Pause();
    }

    public void Resume()
    {
        if (FailedToInitializeOrRun) return;

        shouldBePlaying = true;
        var currentOutputDevice = CurrentOutputDevice;
        if (currentOutputDevice == null)
        {
            ScheduleRecovery("resume requested while output device unavailable");
            return;
        }

        if (currentOutputDevice.PlaybackState != PlaybackState.Paused && currentOutputDevice.PlaybackState != PlaybackState.Stopped) return;

        DebugLogAudioEvent("Resume requested");
        currentOutputDevice.Play();
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

    private static void DebugLogAudioEvent(string message, Exception? ex = null)
    {
        return;
        try
        {
            var lines = new List<string>()
            {
                $"[{DateTimeOffset.Now:O}] {message}",
                $"Thread={Environment.CurrentManagedThreadId}",
                $"Stack={Environment.StackTrace}"
            };

            if (ex != null)
            {
                lines.Add($"Exception={ex}");
            }

            File.AppendAllText(PlaybackDebugLogPath, string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
        }
    }
}

internal sealed class AudioDeviceNotificationClient : IMMNotificationClient
{
    private readonly AudioPlaybackEngine engine;

    public AudioDeviceNotificationClient(AudioPlaybackEngine engine) => this.engine = engine;

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow != DataFlow.Render || role != Role.Multimedia) return;
        engine.NotifyDeviceChanged($"default render device changed to '{defaultDeviceId}'");
    }

    public void OnDeviceAdded(string pwstrDeviceId) => engine.NotifyDeviceChanged($"audio device added: {pwstrDeviceId}");

    public void OnDeviceRemoved(string deviceId) => engine.NotifyDeviceChanged($"audio device removed: {deviceId}");

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => engine.NotifyDeviceChanged($"audio device state changed: {deviceId} -> {newState}");

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) => engine.NotifyDeviceChanged($"audio device property changed: {pwstrDeviceId}");
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

internal sealed class OutputProtectionSampleProvider : ISampleProvider
{
    private const float HeadroomGain = 0.92f;
    private const float SoftClipThreshold = 0.85f;
    private readonly ISampleProvider inner;

    public OutputProtectionSampleProvider(ISampleProvider inner) => this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public WaveFormat WaveFormat => inner.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        var end = offset + read;
        for (var i = offset; i < end; i++)
        {
            buffer[i] = ProtectSample(buffer[i] * HeadroomGain);
        }

        return read;
    }

    private static float ProtectSample(float sample)
    {
        var abs = MathF.Abs(sample);
        if (abs <= SoftClipThreshold) return sample;

        var sign = MathF.Sign(sample);
        var excess = (abs - SoftClipThreshold) / (1f - SoftClipThreshold);
        var shaped = SoftClipThreshold + ((1f - SoftClipThreshold) * MathF.Tanh(excess));
        return sign * MathF.Min(1f, shaped);
    }
}
