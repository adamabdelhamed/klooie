using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie.tests;
[TestClass]
[TestCategory(Categories.Audio)]
public class ScheduledSynthProviderTests
{
    [TestMethod]

    public void TestScheduledSynthProvider()
    {
        int frames = 8; // 8 stereo samples = 16 floats
        var provider =  new ScheduledSignalSourceMixer();

        // Schedule two notes to start at sample 0, each lasting for 'frames' frames
        float voice1Value = 1.0f;
        float voice2Value = 0.5f;
        var voice1 = TestSignalSource.Create(voice1Value, frames);
        var voice2 = TestSignalSource.Create(voice2Value, frames);

        provider.ScheduleNote(ScheduledNoteEvent.Create(
            startSample: 0,
            durationSeconds: (double)frames / SoundProvider.SampleRate,
            voice: voice1
        ));
        provider.ScheduleNote(ScheduledNoteEvent.Create(
            startSample: 0,
            durationSeconds: (double)frames / SoundProvider.SampleRate,
            voice: voice2
        ));

        float[] output = new float[frames * SoundProvider.ChannelCount];
        int read = provider.Read(output, 0, output.Length);

        // Each sample should be (1.0 + 0.5) = 1.5
        for (int i = 0; i < output.Length; i++)
        {
            Assert.AreEqual(1.5f, output[i], 0.0001f, $"Sample {i} incorrect");
        }

        // Optionally: Read again, should be zeroed (both voices done)
        Array.Clear(output, 0, output.Length);
        read = provider.Read(output, 0, output.Length);
        for (int i = 0; i < output.Length; i++)
        {
            Assert.AreEqual(0f, output[i], 0.0001f, $"Sample {i} should be silence after voices end");
        }
    }

 
    public static float MidiNoteToFrequency(int noteNumber)
    {
        return 440f * (float)Math.Pow(2, (noteNumber - 69) / 12.0);
    }
 

    [TestMethod]
    public void OverlappingNotesGetUniqueSynthSignalSourceInstances()
    {
        int sampleRate = 44100, channels = 2;
        var provider = new ScheduledSignalSourceMixer();

        int overlapCount = 4;
        double duration = 0.5;
        List<SynthSignalSource> voices = new();
        List<int> ids = new();

        // Schedule 4 overlapping bass notes, all starting at different times but overlapping in time.
        for (int i = 0; i < overlapCount; i++)
        {
            var patch = SynthPatches.CreateBass();
            var src = CreateNote(
                freq: MidiNoteToFrequency(45 + i),       // C, C#, D, D#
                durationSeconds: duration,
                patch: patch,
                volume: 1f
            );
            voices.Add(src);
            ids.Add(src.Id);

            provider.ScheduleNote(ScheduledNoteEvent.Create(
                startSample: (long)((i * 0.1) * sampleRate), // Each starts 0.1s after the previous, so they all overlap for a bit
                durationSeconds: duration,
                voice: src
            ));
        }

        // Check: Each scheduled voice has a unique Id (no pooling/reuse at scheduling)
        Assert.AreEqual(overlapCount, ids.Distinct().Count(),
            $"Expected {overlapCount} unique SynthSignalSource objects, got {ids.Distinct().Count()}. IDs: {string.Join(", ", ids)}");

        // Optional: render and check overlapping output is not silent
        float[] buffer = new float[(int)(sampleRate * channels * (duration + 0.3))]; // enough for all notes
        provider.Read(buffer, 0, buffer.Length);

        float energy = 0;
        foreach (var val in buffer)
            energy += Math.Abs(val);

        Assert.IsTrue(energy > 10, $"Output buffer too quiet or silent: {energy}");

        // Also check that no two objects are the same (for extra paranoia)
        for (int i = 0; i < voices.Count; i++)
        {
            for (int j = i + 1; j < voices.Count; j++)
            {
                Assert.AreNotSame(voices[i], voices[j], $"Voices at {i} and {j} are the same object!");
            }
        }
    }
    [TestMethod]
    public void VoicesNotDisposedUntilDone_DuringChunkedRender()
    {
        int sampleRate = 44100, channels = 2;
        var provider = new ScheduledSignalSourceMixer();

        // Two long overlapping notes
        var src1 = CreateNote(MidiNoteToFrequency(45), 1.0, SynthPatches.CreateBass(), 1f);
        var src2 = CreateNote(MidiNoteToFrequency(48), 1.0, SynthPatches.CreateBass(), 1f);

        provider.ScheduleNote(ScheduledNoteEvent.Create(0, 1.0, src1));
        provider.ScheduleNote(ScheduledNoteEvent.Create(0, 1.0, src2));

        float[] buffer = new float[256 * channels];
        int totalSamples = 0;
        int reads = 0;
        while (totalSamples < sampleRate * 2) // 1 sec
        {
            int read = provider.Read(buffer, 0, buffer.Length);
            Assert.IsFalse(src1.IsDone && reads == 0, "src1 ended too early!");
            Assert.IsFalse(src2.IsDone && reads == 0, "src2 ended too early!");
            totalSamples += read;
            reads++;
        }
    }

 
    public static SynthSignalSource CreateNote(float freq, double durationSeconds, SynthPatch patch, float volume = 1f)
    {
        var masterKnob = VolumeKnob.Create();
        masterKnob.Volume = volume;
        return SynthSignalSource.Create(freq, patch, masterKnob, null);
    }
}

public class TestSignalSource : SynthSignalSource
{
    private float _value;
    private int _samplesToProduce;
    private int _samplesProduced = 0;


    private TestSignalSource() { }
    private static LazyPool<TestSignalSource> _pool = new(() => new TestSignalSource());
    public static TestSignalSource Create(float value, int samplesToProduce)
    {
        var ret = _pool.Value.Rent();
        ret._value = value;
        ret._samplesToProduce = samplesToProduce;
        return ret;
    }

    public override void ReleaseNote()
    {
        
    }

    public override int Render(float[] buffer, int offset, int count)
    {
        int samplesLeft = _samplesToProduce - _samplesProduced;
        int samplesToWrite = Math.Min(samplesLeft, count / 2); // stereo!
        for (int i = 0; i < samplesToWrite * 2; i++)
            buffer[offset + i] = _value;
        _samplesProduced += samplesToWrite;
        return samplesToWrite * 2;
    }
}