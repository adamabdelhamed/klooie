using NAudio.Wave;
using PowerArgs;
using System;
using System.Collections.Generic;

namespace klooie;

public class AudioNoteInput : Recyclable, IAudioNoteInput
{
    private WaveInEvent waveIn;
    private BufferedWaveProvider buffer;
    private Event<IMidiEvent>? midiFired;
    public Event<IMidiEvent> EventFired => midiFired ??= Event<IMidiEvent>.Create();

    private float[] recordingBuffer = new float[44100 * 10]; // up to 10s buffer
    private int sampleRate = 44100;
    private int bufferPos = 0;
    private NoiseProfiler noiseProfiler;
    private BeatAlignedSegmenter segmenter;
    private int? currentNoteNumber;
    private int currentNoteStartIndex;

    private Lock lck = new();
    private ConsoleApp app;

    private static LazyPool<AudioNoteInput> pool = new(() => new AudioNoteInput());
    private AudioNoteInput() { }
    public static AudioNoteInput Create()
    {
        var input = pool.Value.Rent();
        input.Initialize();
        return input;
    }

    private void WriteLine(ConsoleString s) => SoundProvider.Current.EventLoop.Invoke(()=> ConsoleApp.Current?.WriteLine(s));
    private void WriteLine(string s) => WriteLine(s.ToWhite());
    private void Initialize()
    {
        app = ConsoleApp.Current ?? throw new InvalidOperationException("AudioNoteInput requires ConsoleApp");
        waveIn = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(sampleRate, 1) // mono, 44.1kHz
        };

        waveIn.DataAvailable += OnDataAvailable;
        buffer = new BufferedWaveProvider(waveIn.WaveFormat);
        noiseProfiler = new NoiseProfiler(sampleRate, durationSeconds: 1f);

        segmenter = new BeatAlignedSegmenter(bpm: 60, sampleRate: sampleRate, subdivisionsPerBeat: 1);
        segmenter.SegmentReady.Subscribe(OnSegmentReady, this);


    }

    private float ComputeAverageAbsAmplitude(float[] segment)
    {
        float sum = 0;
        for (int i = 0; i < segment.Length; i++)
            sum += Math.Abs(segment[i]);
        return sum / segment.Length;
    }

    private void OnSegmentReady((float[] s, int w) tuple)
    {
        var segment = tuple.s;
        var windowIndex = tuple.w;

        float avg = ComputeAverageAbsAmplitude(segment);

        if (IsSilent(segment))
        {
            WriteLine("[Debug] Segment rejected: silent.".ToGray());
            // End current note if one is active
            if (currentNoteNumber.HasValue)
            {
                FireNoteOff(currentNoteNumber.Value, currentNoteStartIndex, windowIndex);
                currentNoteNumber = null;
            }
            return;
        }

        float freq = PitchDetector.EstimatePitch(segment, sampleRate);
        WriteLine($"[Debug] Frequency is {freq}.".ToGreen());
        if (freq <= 0) return;

        int midiNote = FrequencyToMidi(freq);

        if (currentNoteNumber == null)
        {
            currentNoteNumber = midiNote;
            currentNoteStartIndex = windowIndex;
        }
        else if (Math.Abs(currentNoteNumber.Value - midiNote) >= 2)
        {
            // End previous note
            FireNoteOff(currentNoteNumber.Value, currentNoteStartIndex, windowIndex);
            // Start new one
            currentNoteNumber = midiNote;
            currentNoteStartIndex = windowIndex;
        }
    }

    private void FireNoteOff(int noteNumber, int startIndex, int endIndex)
    {
        int durationBeats = endIndex - startIndex;

        // Approximate start sample to give external tools a real-world offset
        int samplesPerBeat = segmenter.SamplesPerBeat; // you'll need to expose this
        int sampleOffset = startIndex * samplesPerBeat;
        SoundProvider.Current.EventLoop.Invoke(() =>
        {
            EventFired.Fire(new MidiNoteDetected
            {
                NoteNumber = noteNumber,
                Velocity = 100, // optional velocity detection later
                Command = MidiCommand.NoteOn,
                StartSampleIndex = sampleOffset
            });

            EventFired.Fire(new MidiNoteDetected
            {
                NoteNumber = noteNumber,
                Velocity = 0,
                Command = MidiCommand.NoteOff,
                StartSampleIndex = endIndex * segmenter.SamplesPerBeat
            });
        });
    }

    private int FrequencyToMidi(float freqHz) =>
    (int)Math.Round(69 + 12 * Math.Log2(freqHz / 440.0));


    public void Start() => waveIn.StartRecording();
    public void Stop() => waveIn.StopRecording();

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i);
            float normalized = sample / 32768f;

            recordingBuffer[bufferPos++] = normalized;

            if (!noiseProfiler.IsComplete)
            {
                noiseProfiler.AddSample(normalized);
                if (noiseProfiler.IsComplete)
                {
                    WriteLine($"[AudioNoteInput] Noise floor: {noiseProfiler.NoiseFloor:F6}");
                }
            }

            if (noiseProfiler.IsComplete)
                segmenter.AddSample(normalized);

            if (bufferPos >= recordingBuffer.Length)
                bufferPos = 0;
        }

    }

    private bool IsSilent(float[] segment)
    {
        float avg = ComputeAverageAbsAmplitude(segment);
        float floor = noiseProfiler.NoiseFloor;
        float cutoff = floor * 1.01f;

        bool silent = avg < cutoff;

       
        return silent;
    }



    protected override void OnReturn()
    {
        Stop();
        waveIn.Dispose();
        waveIn = null!;
        midiFired = null;
        bufferPos = 0;
        base.OnReturn();
    }
}

public class MidiNoteDetected : IMidiEvent
{
    public int NoteNumber { get; set; }
    public int Velocity { get; set; }
    public MidiCommand Command { get; set; } = MidiCommand.NoteOn;
    public int StartSampleIndex { get; set; } 
}

public sealed class NoiseProfiler
{
    private readonly int sampleRate;
    private readonly int durationSamples;
    private float accumulated;
    private int count;
    public bool IsComplete => count >= durationSamples;
    public float NoiseFloor { get; private set; }

    public NoiseProfiler(int sampleRate, float durationSeconds = 1f)
    {
        this.sampleRate = sampleRate;
        this.durationSamples = (int)(sampleRate * durationSeconds);
    }

    public void AddSample(float sample)
    {
        if (IsComplete) return;
        accumulated += Math.Abs(sample);
        count++;

        if (IsComplete)
        {
            NoiseFloor = accumulated / count;
        }
    }

    public void Reset()
    {
        accumulated = 0;
        count = 0;
        NoiseFloor = 0;
    }
}

public sealed class BeatAlignedSegmenter
{
    private readonly int sampleRate;
    private readonly int samplesPerWindow;
    private readonly List<float> buffer = new();
    private int windowIndex = 0;

    public int SamplesPerBeat { get; }

    public Event<(float[] segment, int windowIndex)> SegmentReady = Event<(float[] segment, int windowIndex)>.Create();

    public BeatAlignedSegmenter(int bpm, int sampleRate, int subdivisionsPerBeat = 1)
    {
        this.sampleRate = sampleRate;

        float secondsPerBeat = 60f / bpm;
        SamplesPerBeat = (int)(sampleRate * secondsPerBeat);
        float secondsPerSubdivision = secondsPerBeat / subdivisionsPerBeat;
        samplesPerWindow = (int)(sampleRate * secondsPerSubdivision);
    }

    public void AddSample(float sample)
    {
        buffer.Add(sample);
        if (buffer.Count >= samplesPerWindow)
        {
            var segment = buffer.Take(samplesPerWindow).ToArray();
            buffer.RemoveRange(0, samplesPerWindow);
            SegmentReady.Fire((segment, windowIndex++));
        }
    }

    public void Reset()
    {
        buffer.Clear();
        windowIndex = 0;
    }
}

public static class PitchDetector
{
    public static float EstimatePitch(float[] buffer, int sampleRate, int minHz = 80, int maxHz = 1000, float confidenceThreshold = 0.1f)
    {
        int minLag = sampleRate / maxHz;
        int maxLag = sampleRate / minHz;

        float bestCorrelation = 0f;
        int bestLag = -1;

        float selfEnergy = 0f;
        for (int i = 0; i < buffer.Length; i++)
            selfEnergy += buffer[i] * buffer[i];

        if (selfEnergy < 1e-5f) return -1; // prevent divide-by-zero and handle silence

        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float corr = 0f;
            for (int i = 0; i < buffer.Length - lag; i++)
                corr += buffer[i] * buffer[i + lag];

            if (corr > bestCorrelation)
            {
                bestCorrelation = corr;
                bestLag = lag;
            }
        }

        if (bestLag == -1) return -1;

        float confidence = bestCorrelation / selfEnergy;
        if (confidence < confidenceThreshold) return -1;

        return sampleRate / (float)bestLag;
    }

}