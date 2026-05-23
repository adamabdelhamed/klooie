using Microsoft.JSInterop;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserSoundProvider : ISoundProvider
{
    private readonly IJSRuntime js;
    private readonly ScheduledSignalSourceMixer scheduledSignalMixer = new(ScheduledSignalMixerMode.PreRenderOnly);

    public BrowserSoundProvider(IJSRuntime js)
    {
        this.js = js;
        MasterVolume = VolumeKnob.Create();
    }

    public VolumeKnob MasterVolume { get; }
    public EventLoop EventLoop => ConsoleApp.Current!;
    public long SamplesRendered => 0;
    public IConsoleAudioRecordingSink? AudioRecordingSink { get; set; }
    public ScheduledSignalSourceMixer ScheduledSignalMixer => scheduledSignalMixer;
    public bool FailedToInitializeOrRun => false;

    public ILifetime Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null, bool isMusic = false)
    {
        if (string.IsNullOrWhiteSpace(sound)) return Lifetime.Completed;

        var assetName = Path.HasExtension(sound) ? sound : sound + ".mp3";
        var volume = Math.Clamp((volumeKnob?.Volume ?? 1f) * MasterVolume.Volume, 0f, 1f);
        _ = js.InvokeVoidAsync("klooieAssets.play", BrowserAssetProvider.ToAssetUrl(assetName), volume, isMusic);
        return Lifetime.Completed;
    }

    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null, bool isMusic = false) { }
    public void Pause() { }
    public void Resume() { }
    public void ClearCache() { }
    public IReleasableNote? PlaySustainedNote(NoteExpression note) => null;
    public Task Play(Song song, ILifetime? lifetime = null) => Task.CompletedTask;
}
