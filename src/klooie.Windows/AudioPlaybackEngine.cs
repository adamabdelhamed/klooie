using klooie.Gaming;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Concurrent;
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
            scheduledSynthProvider = new ScheduledSynthProvider(SampleRate, ChannelCount); // We'll define this class next
            mixer = new MixingSampleProvider(new ISampleProvider[] { sfxMixer, scheduledSynthProvider }) { ReadFully = true };

            outputDevice = new WasapiOut(AudioClientShareMode.Shared, false, 300);
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

    public IReleasableNote PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null)
    {
        patch.Velocity = knob?.Volume ?? 1f;
        var source = SynthSignalSource.Create(frequencyHz, patch, MasterVolume, knob);
        var voice = SynthVoiceProvider.Create(source);
        mixer.AddMixerInput(voice);
        var scheduler = Game.Current?.PausableScheduler ?? ConsoleApp.Current.Scheduler;
        scheduler.Delay(durationSeconds * 1000, voice.ReleaseNote);
        return voice;
    }

    public IReleasableNote PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null)
    {
        patch.Velocity = knob?.Volume ?? 1f;
        var source = SynthSignalSource.Create(frequencyHz, patch, MasterVolume, knob);
        var voice = SynthVoiceProvider.Create(source);
        mixer.AddMixerInput(voice);
        return voice;
    }

    public void ScheduleSynthNote(
    int midiNote,
    long startSample,
    double durationSeconds,
    float velocity = 1.0f,
    SynthPatch patch = null)
    {
        float freq = MIDIInput.MidiNoteToFrequency(midiNote);
        var knob = VolumeKnob.Create();
        knob.Volume = velocity;
        var p = patch ?? SynthPatches.CreateBass();
        var source = SynthSignalSource.Create(freq, p, MasterVolume, knob);
        var voice = SynthVoiceProvider.Create(source);
        scheduledSynthProvider.ScheduleNote(ScheduledNoteEvent.Create(startSample, durationSeconds,  voice));
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

public class ScheduledNoteEvent : Recyclable
{
    public long StartSample; // Absolute sample offset
    public SynthVoiceProvider Voice; 
    public double DurationSeconds;

    private static LazyPool<ScheduledNoteEvent> pool = new LazyPool<ScheduledNoteEvent>(() => new ScheduledNoteEvent());
    protected ScheduledNoteEvent() { }
    public static ScheduledNoteEvent Create(long startSample, double durationSeconds, SynthVoiceProvider voice)
    {
        var ret = pool.Value.Rent();
        ret.StartSample = startSample;
        ret.DurationSeconds = durationSeconds;
        ret.Voice = voice;
        return ret;
    }

}

public class ScheduledSynthProvider : ISampleProvider
{
    private readonly WaveFormat waveFormat;
    private readonly ConcurrentQueue<ScheduledNoteEvent> scheduledNotes = new();
    private readonly List<(SynthVoiceProvider Voice, long StartSample, int SamplesPlayed, long ReleaseSample, bool Released)> activeVoices = new();
    private long samplesRendered = 0;

    public long SamplesRendered => samplesRendered;
    public ScheduledSynthProvider(int sampleRate, int channels)
    {
        waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    public WaveFormat WaveFormat => waveFormat;
    public void ScheduleNote(ScheduledNoteEvent note) => scheduledNotes.Enqueue(note);

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = waveFormat.Channels;
        int samplesRequested = count / channels;
        long bufferStart = samplesRendered;
        long bufferEnd = bufferStart + samplesRequested;

        // 1. Promote any scheduled notes whose start time lands in or before this buffer
        while (scheduledNotes.TryPeek(out var note) && note.StartSample < bufferEnd)
        {
            scheduledNotes.TryDequeue(out note);
            var voice = note.Voice;
            int durSamples = (int)(note.DurationSeconds * waveFormat.SampleRate);
            long releaseSample = note.StartSample + durSamples;
            activeVoices.Add((voice, note.StartSample, 0, releaseSample, false));
            note.Dispose();
        }

        Array.Clear(buffer, offset, count);
        var scratch = System.Buffers.ArrayPool<float>.Shared.Rent(count);

        // 2. Mix active voices
        for (int v = activeVoices.Count - 1; v >= 0; v--)
        {
            var (voice, startSample, samplesPlayed, releaseSample, released) = activeVoices[v];

            // Calculate where the voice's next sample lands in this buffer
            long voiceAbsoluteSample = startSample + samplesPlayed;

            // If the voice starts after this buffer, skip it for now
            if (voiceAbsoluteSample >= bufferEnd)
                continue;

            // Determine where to start mixing in the output buffer
            int bufferWriteOffset = (int)Math.Max(0, voiceAbsoluteSample - bufferStart);
            // Determine how many samples from the voice to skip (if the buffer starts before the voice)
            int voiceReadOffset = (int)Math.Max(0, bufferStart - startSample);

            // The max samples we can mix from this voice into this buffer
            int samplesAvailable = samplesRequested - bufferWriteOffset;
            if (samplesAvailable <= 0)
                continue;

            // Release note if needed
            if (!released && voiceAbsoluteSample >= releaseSample)
            {
                voice.ReleaseNote();
                released = true;
            }

            // Read from the voice: always start from where the voice itself left off
            int floatsNeeded = samplesAvailable * channels;
            int read = voice.Read(scratch, 0, floatsNeeded);

            // Mix into the output buffer at the correct offset
            int bufferMixIndex = offset + bufferWriteOffset * channels;
            for (int i = 0; i < read; i++)
            {
                buffer[bufferMixIndex + i] += scratch[i];
            }

            samplesPlayed += read / channels;

            // Check if voice is done
            bool done = voice.IsDone;
            if (done)
            {
                voice.Dispose();
                activeVoices.RemoveAt(v);
            }
            else
            {
                activeVoices[v] = (voice, startSample, samplesPlayed, releaseSample, released);
            }
        }

        System.Buffers.ArrayPool<float>.Shared.Return(scratch);
        samplesRendered += samplesRequested;
        return count;
    }

}
