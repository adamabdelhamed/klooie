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

    private void WithSpawnedVoices(NoteExpression note, Action<ISynthPatch, RecyclableList<SynthSignalSource>> action)
    {
        var patch = note.Instrument?.PatchFunc() ?? ElectricGuitar.Create();
        patch.WithVolume(note.Velocity / 127f);
        if (!patch.IsNotePlayable(note.MidiNote))
        {
            ConsoleApp.Current?.WriteLine(ConsoleString.Parse($"Note [Red]{note.MidiNote}[D] is not playable by the current instrument"));
            return;
        }

        RecyclableList<SynthSignalSource> voices = RecyclableListPool<SynthSignalSource>.Instance.Rent(8);
        try
        {
            var tempEvent = ScheduledNoteEvent.Create(note, patch);
            patch.SpawnVoices(MIDIInput.MidiNoteToFrequency(note.MidiNote), MasterVolume, tempEvent, voices.Items);
            VoiceCountTracker.Track(voices.Items);
            action(patch, voices);
            tempEvent.Dispose();
        }
        finally
        {
            voices.Dispose();
        }
    }

    public RecyclableList<IReleasableNote> PlaySustainedNote(NoteExpression note)
    {
        RecyclableList<IReleasableNote>? result = null;
        WithSpawnedVoices(note, (patch, voices) =>
        {
            result = RecyclableListPool<IReleasableNote>.Instance.Rent(voices.Count);
            for (int i = 0; i < voices.Items.Count; i++)
            {
                var voice = SynthVoiceProvider.Create(voices.Items[i]);
                result.Items.Add(voice);
                mixer.AddMixerInput(voice);
            }
        });
        return result ?? RecyclableListPool<IReleasableNote>.Instance.Rent(0);
    }

    public void Play(Song song, ILifetime? lifetime = null)
    {
        var tracks = new Dictionary<string, RecyclableList<ScheduledNoteEvent>>();
        for (int i = 0; i < song.Count; i++)
        {
            var note = song[i];
            var trackKey = note.Instrument?.Name ?? "Default";
            if (tracks.TryGetValue(trackKey, out var track) == false)
            {
                track = RecyclableListPool<ScheduledNoteEvent>.Instance.Rent(song.Count * 8);
                tracks[trackKey] = track;
            }

            var patch = note.Instrument?.PatchFunc() ?? ElectricGuitar.Create();
            patch.WithVolume(note.Velocity / 127f);
            if (!patch.IsNotePlayable(note.MidiNote))
            {
                ConsoleApp.Current?.WriteLine(ConsoleString.Parse($"Note [Red]{note.MidiNote}[D] is not playable by the current instrument"));
                continue;
            }

            var scheduledNote = ScheduledNoteEvent.Create(note, patch);
            track.Items.Add(scheduledNote);
            if (lifetime != null)
            {
                var tracker = LeaseHelper.Track(scheduledNote);
                lifetime.OnDisposed(tracker, static t =>
                {
                    if (t.IsRecyclableValid)
                    {
                        t.Recyclable!.Cancel();
                    }
                    t.Dispose();
                });
            }
        }

        foreach(var track in tracks.Values)
        {
            if(track.Count == 0)
            {
                track.Dispose();
                continue;
            }
            scheduledSynthProvider.ScheduleTrack(track);
        }
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

internal class VoiceCountTracker : Recyclable
{
    private static LazyPool<VoiceCountTracker> _pool = new(() => new VoiceCountTracker());
    private int remainingVoices;
    private VoiceCountTracker() { }

    public static VoiceCountTracker Track(List<SynthSignalSource> voices)
    {
        var tracker = _pool.Value.Rent();
        tracker.remainingVoices = voices.Count;

        for(int i = 0; i < voices.Count; i++)
        {
            var voice = voices[i];
            voice.OnDisposed(tracker, static me =>
            {
                me.remainingVoices--;
                if (me.remainingVoices <= 0)
                {
                    me.Dispose();
                }
            });
        }

        return tracker;
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
