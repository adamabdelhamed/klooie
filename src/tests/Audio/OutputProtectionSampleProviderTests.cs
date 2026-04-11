using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;
using System;

namespace klooie.tests;

[TestClass]
public class OutputProtectionSampleProviderTests
{
    [TestMethod]
    public void OutputProtection_ReducesSustainedOverloadAggressively()
    {
        var frames = 4096;
        var source = new ConstantSampleProvider(1f, frames);
        var protector = new OutputProtectionSampleProvider(source);
        var buffer = new float[frames * 2];

        var read = protector.Read(buffer, 0, buffer.Length);

        Assert.AreEqual(buffer.Length, read);
        var lastLeftSample = MathF.Abs(buffer[read - 2]);
        var lastRightSample = MathF.Abs(buffer[read - 1]);
        Assert.IsTrue(lastLeftSample < 0.72f, $"Expected sustained limiting to pull left sample well below clipping, but got {lastLeftSample}");
        Assert.IsTrue(lastRightSample < 0.72f, $"Expected sustained limiting to pull right sample well below clipping, but got {lastRightSample}");
    }

    [TestMethod]
    public void OutputProtection_RecoversGraduallyAfterBurst()
    {
        var hotFrames = 2048;
        var quietFrames = 2048;
        var source = new SegmentedSampleProvider((1f, hotFrames), (0.2f, quietFrames));
        var protector = new OutputProtectionSampleProvider(source);
        var buffer = new float[(hotFrames + quietFrames) * 2];

        var read = protector.Read(buffer, 0, buffer.Length);

        Assert.AreEqual(buffer.Length, read);
        var firstQuietLeft = MathF.Abs(buffer[hotFrames * 2]);
        var laterQuietLeft = MathF.Abs(buffer[(hotFrames * 2) + 400]);
        Assert.IsTrue(firstQuietLeft < 0.16f, $"Expected protection to keep gain reduction briefly after overload, but got {firstQuietLeft}");
        Assert.IsTrue(laterQuietLeft > firstQuietLeft, $"Expected gain to recover over time, but first quiet sample was {firstQuietLeft} and later sample was {laterQuietLeft}");
    }

    private sealed class ConstantSampleProvider : ISampleProvider
    {
        private readonly float value;
        private int samplesRemaining;

        public ConstantSampleProvider(float value, int frames)
        {
            this.value = value;
            samplesRemaining = frames * 2;
        }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount);

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesToWrite = Math.Min(count, samplesRemaining);
            for (var i = 0; i < samplesToWrite; i++)
            {
                buffer[offset + i] = value;
            }

            samplesRemaining -= samplesToWrite;
            return samplesToWrite;
        }
    }

    private sealed class SegmentedSampleProvider : ISampleProvider
    {
        private readonly (float value, int frames)[] segments;
        private int segmentIndex;
        private int framesWrittenInSegment;

        public SegmentedSampleProvider(params (float value, int frames)[] segments) => this.segments = segments;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SoundProvider.SampleRate, SoundProvider.ChannelCount);

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesWritten = 0;
            while (samplesWritten < count && segmentIndex < segments.Length)
            {
                var (value, frames) = segments[segmentIndex];
                var remainingFrames = frames - framesWrittenInSegment;
                if (remainingFrames <= 0)
                {
                    segmentIndex++;
                    framesWrittenInSegment = 0;
                    continue;
                }

                var framesToWrite = Math.Min((count - samplesWritten) / 2, remainingFrames);
                for (var i = 0; i < framesToWrite; i++)
                {
                    buffer[offset + samplesWritten++] = value;
                    buffer[offset + samplesWritten++] = value;
                }

                framesWrittenInSegment += framesToWrite;
            }

            return samplesWritten;
        }
    }
}
