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