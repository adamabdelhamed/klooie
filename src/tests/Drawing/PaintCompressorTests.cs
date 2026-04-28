using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System.Collections.Generic;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Drawing)]
public class PaintCompressorTests
{
    [TestMethod]
    public void PaintCompressor_UsesCentroidToCompressGentleGradients()
    {
        var previousDetail = PaintDetailSettings.DetailPercent;
        PaintDetailSettings.DetailPercent = PaintDetailSettings.DefaultDetailPercent;
        try
        {
            var pixels = new ConsoleCharacter[8];
            for (int i = 0; i < pixels.Length; i++)
            {
                var color = new RGB((byte)i, 0, 0);
                pixels[i] = new ConsoleCharacter('X', color, RGB.Black);
            }

            int threshold = PaintCompressor.ComputeColorThresholdSq();
            PaintCompressor.BuildPerFrameThresholds(threshold);
            var runs = new List<Run>();
            PaintCompressor.BuildRunsForLine(pixels, pixels.Length, 0, threshold, runs);

            Assert.IsTrue(runs.Count < pixels.Length);
            Assert.IsTrue(runs[0].Length > 2);
            Assert.AreNotEqual(pixels[0].ForegroundColor, runs[0].FG);
        }
        finally
        {
            PaintDetailSettings.DetailPercent = previousDetail;
        }
    }

    [TestMethod]
    public void PaintCompressor_IgnoresInvisibleForegroundOnSpaces()
    {
        var pixels = new[]
        {
            new ConsoleCharacter(' ', RGB.Red, RGB.Blue),
            new ConsoleCharacter(' ', RGB.Green, RGB.Blue),
            new ConsoleCharacter(' ', RGB.Yellow, RGB.Blue),
        };

        PaintCompressor.BuildPerFrameThresholds(0);
        var runs = new List<Run>();
        PaintCompressor.BuildRunsForLine(pixels, pixels.Length, 0, 0, runs);

        Assert.AreEqual(1, runs.Count);
        Assert.AreEqual(3, runs[0].Length);
        Assert.AreEqual(RGB.Blue, runs[0].BG);
    }

    [TestMethod]
    public void PaintCompressor_LowDetailCompressesColorRampsMoreAggressively()
    {
        var previousDetail = PaintDetailSettings.DetailPercent;
        try
        {
            var pixels = new ConsoleCharacter[20];
            for (int i = 0; i < pixels.Length; i++)
            {
                var color = new RGB((byte)(30 + (i * 4)), 40, 50);
                pixels[i] = new ConsoleCharacter('X', color, RGB.Black);
            }

            int defaultRunCount = CountRunsAtDetail(PaintDetailSettings.DefaultDetailPercent, pixels);
            int lowDetailRunCount = CountRunsAtDetail(0, pixels);

            Assert.IsTrue(lowDetailRunCount < defaultRunCount);
        }
        finally
        {
            PaintDetailSettings.DetailPercent = previousDetail;
        }
    }

    private static int CountRunsAtDetail(int detailPercent, ConsoleCharacter[] pixels)
    {
        PaintDetailSettings.DetailPercent = detailPercent;
        int threshold = PaintCompressor.ComputeColorThresholdSq();
        PaintCompressor.BuildPerFrameThresholds(threshold);
        var runs = new List<Run>();
        PaintCompressor.BuildRunsForLine(pixels, pixels.Length, 0, threshold, runs);
        return runs.Count;
    }
}
