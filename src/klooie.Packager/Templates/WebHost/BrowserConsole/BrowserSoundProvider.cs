using Microsoft.JSInterop;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserSoundProvider : ISoundProvider, IAudioPlaybackPositionProvider
{
    private readonly IJSRuntime js;
    private readonly ScheduledSignalSourceMixer scheduledSignalMixer = new(ScheduledSignalMixerMode.PreRenderOnly);
    private readonly Dictionary<long, BrowserSoundPlayback> activePlaybacks = new();
    private readonly DotNetObjectReference<BrowserSoundProvider> dotNetReference;
    private long nextPlaybackId;
    private bool paused;

    public BrowserSoundProvider(IJSRuntime js)
    {
        this.js = js;
        dotNetReference = DotNetObjectReference.Create(this);
        MasterVolume = VolumeKnob.Create();
    }

    public VolumeKnob MasterVolume { get; }
    public Event<AudioPlaybackPosition> PlaybackPositionChanged { get; } = Event<AudioPlaybackPosition>.Create();
    public EventLoop EventLoop => ConsoleApp.Current!;
    public long SamplesRendered => 0;
    public IConsoleAudioRecordingSink? AudioRecordingSink { get; set; }
    public ScheduledSignalSourceMixer ScheduledSignalMixer => scheduledSignalMixer;
    public bool FailedToInitializeOrRun => false;

    public ILifetime Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (string.IsNullOrWhiteSpace(sound)) return Lifetime.Completed;
        if (maxDuration == null && volumeKnob == null)
        {
            var assetName = Path.HasExtension(sound) ? sound : sound + ".mp3";
            _ = js.InvokeVoidAsync(
                "klooieAssets.play",
                AllocatePlaybackId(),
                BrowserAssetProvider.ToAssetUrl(assetName),
                MixVolume(null),
                MixPan(null),
                false,
                isMusic,
                paused,
                isMusic ? dotNetReference : null);
            return Lifetime.Completed;
        }

        var handle = BrowserSoundPlayback.Create(this, sound, volumeKnob, maxDuration, loop: false, isMusic);
        return handle;
    }

    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (string.IsNullOrWhiteSpace(sound)) return;

        _ = BrowserSoundPlayback.Create(this, sound, volumeKnob, duration, loop: true, isMusic);
    }

    public void Pause()
    {
        paused = true;
        _ = js.InvokeVoidAsync("klooieAssets.pauseAll");
    }

    public void Resume()
    {
        paused = false;
        _ = js.InvokeVoidAsync("klooieAssets.resumeAll");
    }

    public void ClearCache() => _ = js.InvokeVoidAsync("klooieAssets.clearAudioCache");
    public IReleasableNote? PlaySustainedNote(NoteExpression note) => null;
    public Task Play(Song song, ILifetime? lifetime = null) => Task.CompletedTask;

    private long AllocatePlaybackId() => Interlocked.Increment(ref nextPlaybackId);

    [JSInvokable]
    public void AudioEnded(long playbackId)
    {
        if (activePlaybacks.TryGetValue(playbackId, out var playback))
        {
            playback.TryDispose("Browser audio ended");
        }
    }

    [JSInvokable]
    public void AudioPositionChanged(long playbackId, string trackId, double timeSeconds, bool isMusic)
    {
        PlaybackPositionChanged.Fire(new AudioPlaybackPosition(playbackId, trackId, Math.Max(0, timeSeconds), isMusic));
    }

    private float MixVolume(VolumeKnob? volumeKnob)
    {
        var knobVolume = volumeKnob?.Volume ?? 1f;
        return Math.Clamp(knobVolume * MasterVolume.Volume, 0f, 1f);
    }

    private float MixPan(VolumeKnob? volumeKnob) => Math.Clamp(volumeKnob?.Pan ?? 0f, -1f, 1f);

    private sealed class BrowserSoundPlayback : Recyclable
    {
        private BrowserSoundProvider? provider;
        private VolumeKnob? volumeKnob;
        private long id;

        private BrowserSoundPlayback() { }

        public static BrowserSoundPlayback Create(BrowserSoundProvider provider, string sound, VolumeKnob? volumeKnob, ILifetime? lifetime, bool loop, bool isMusic)
        {
            var playback = new BrowserSoundPlayback
            {
                provider = provider,
                volumeKnob = volumeKnob,
                id = provider.AllocatePlaybackId(),
            };

            if (lifetime != null && lifetime.IsStillValid(lifetime.Lease) == false)
            {
                playback.Dispose("BrowserSoundProvider lifetime already ended");
                return playback;
            }

            if (lifetime != null)
            {
                lifetime.OnDisposed(playback, static p => p.TryDispose("BrowserSoundProvider lifetime ended"));
            }

            volumeKnob?.VolumeChanged.Subscribe(playback, static (p, _) => p.UpdateMix(), playback);
            volumeKnob?.PanChanged.Subscribe(playback, static (p, _) => p.UpdateMix(), playback);
            provider.MasterVolume.VolumeChanged.Subscribe(playback, static (p, _) => p.UpdateMix(), playback);
            provider.activePlaybacks[playback.id] = playback;

            var assetName = Path.HasExtension(sound) ? sound : sound + ".mp3";
            _ = provider.js.InvokeVoidAsync(
                "klooieAssets.play",
                playback.id,
                BrowserAssetProvider.ToAssetUrl(assetName),
                provider.MixVolume(volumeKnob),
                provider.MixPan(volumeKnob),
                loop,
                isMusic,
                provider.paused,
                isMusic || loop == false ? provider.dotNetReference : null);

            return playback;
        }

        private void UpdateMix()
        {
            if (provider == null) return;

            _ = provider.js.InvokeVoidAsync(
                "klooieAssets.update",
                id,
                provider.MixVolume(volumeKnob),
                provider.MixPan(volumeKnob));
        }

        protected override void OnReturn()
        {
            if (provider != null)
            {
                provider.activePlaybacks.Remove(id);
                _ = provider.js.InvokeVoidAsync("klooieAssets.stop", id);
            }

            volumeKnob?.TryDispose("BrowserSoundProvider playback ended");
            provider = null;
            volumeKnob = null;
            id = 0;
            base.OnReturn();
        }
    }
}
