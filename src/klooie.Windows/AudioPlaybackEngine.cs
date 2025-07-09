using klooie.Gaming;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace klooie;

public abstract class AudioPlaybackEngine : ISoundProvider
{
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
            mixer = new MixingSampleProvider(new ISampleProvider[] { sfxMixer, scheduledSynthProvider }) { ReadFully = true };

            outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 200);
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

    public void PlayTimedNote(Note note, VolumeKnob? knob = null)
    {
        RecyclableList<SynthSignalSource> voices = RecyclableListPool<SynthSignalSource>.Instance.Rent(8);
        try
        {
            var p = note.Patch ?? SynthPatches.CreateBass();
            p.SpawnVoices(MIDIInput.MidiNoteToFrequency(note.MidiNode), MasterVolume, knob, voices.Items);

            for (int i = 0; i < voices.Items.Count; i++)
            {
                var voice = SynthVoiceProvider.Create(voices.Items[i]);
                mixer.AddMixerInput(voice);
                var scheduler = Game.Current?.PausableScheduler ?? ConsoleApp.Current.Scheduler;
                scheduler.Delay(note.Duration.TotalSeconds, voice.ReleaseNote);
            }
        }
        finally
        {
            voices.Dispose();
        }
    }

    public RecyclableList<IReleasableNote> PlaySustainedNote(Note note, VolumeKnob? knob)
    {
        RecyclableList<SynthSignalSource> voices = RecyclableListPool<SynthSignalSource>.Instance.Rent(8);
        try
        {
            var p = note.Patch ?? SynthPatches.CreateBass();
            p.SpawnVoices(MIDIInput.MidiNoteToFrequency(note.MidiNode), MasterVolume, knob, voices.Items);
            var releaseable = RecyclableListPool<IReleasableNote>.Instance.Rent(voices.Count);
            for (int i = 0; i < voices.Items.Count; i++)
            {
                var voice = SynthVoiceProvider.Create(voices.Items[i]);
                releaseable.Items.Add(voice);
                mixer.AddMixerInput(voice);
            }
            return releaseable;
        }
        finally
        {
            voices.Dispose();
        }
    }

    private static Comparison<Note> MelodyNoteComparer = (a, b) => a.Start.CompareTo(b.Start);

    public void Play(List<Note> notes)
    {
        const double bufferDelaySeconds = 0.1; // 100ms buffer for safety
        long scheduleZero = SamplesRendered + (long)(bufferDelaySeconds * SoundProvider.SampleRate);
        notes.Sort(MelodyNoteComparer);
        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            long startSample = scheduleZero + (long)Math.Round(note.Start.TotalSeconds * SoundProvider.SampleRate);
            ScheduleSynthNote(note.MidiNode, startSample, note.Duration.TotalSeconds, note.Velocity, note.Patch ?? SynthPatches.CreateBass());
        }
    }


    public void ScheduleSynthNote(
        int midiNote,
        long startSample,
        double durationSeconds,
        float velocity = 1.0f,
        ISynthPatch patch = null)
    {
        float freq = MIDIInput.MidiNoteToFrequency(midiNote);
        var knob = VolumeKnob.Create();
        knob.Volume = velocity/127f;
        var p = patch ?? SynthPatches.CreateBass();

        // Let's say max 4 voices
        RecyclableList<SynthSignalSource> voices = RecyclableListPool<SynthSignalSource>.Instance.Rent(8);
        try
        {
            p.SpawnVoices(freq, MasterVolume, knob, voices.Items);

            for (int i = 0; i < voices.Items.Count; i++)
            {
                scheduledSynthProvider.ScheduleNote(
                    ScheduledNoteEvent.Create(startSample, durationSeconds, voices[i]));
            }
        }
        finally
        {
            voices.Dispose();
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



public class ScheduledSynthProvider : ScheduledSignalSourceMixer, ISampleProvider
{
    private readonly WaveFormat waveFormat;

    public ScheduledSynthProvider() 
    {
        waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount);
    }

    public WaveFormat WaveFormat => waveFormat;
}
