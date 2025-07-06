
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class ConsoleMathTests
{

    [TestMethod]
    public void ConsoleMath_RoundFloat()
    {
        var disagreed = 0;
        var agreed = 0;
        for(var someRandomFloat = 0f; someRandomFloat < 100; someRandomFloat+=.5f)
        {
            var midPointForThisNumber = Math.Floor(someRandomFloat) + .5f;
            var roundedRight = ConsoleMath.Round(someRandomFloat);
            var roundedLame = (int)Math.Round(someRandomFloat);

            if (someRandomFloat < midPointForThisNumber)
            {
                Assert.AreEqual(Math.Floor(someRandomFloat), roundedRight);
            }
            else
            {
                Assert.AreEqual(Math.Ceiling(someRandomFloat), roundedRight);
            }

            if(roundedRight != roundedLame)
            {
                disagreed++;
            }
            else
            {
                agreed++;
            }
        }
        Assert.IsTrue(disagreed > 0);
        Console.WriteLine($"The 2 rounding modes agreed {agreed} times and disagreed {disagreed} times");
    }

    [TestMethod]
    public void ConsoleMath_RoundDouble()
    {
        var disagreed = 0;
        var agreed = 0;
        for (var someRandomDouble = 0.0; someRandomDouble < 100.0; someRandomDouble += .5)
        {
            var midPointForThisNumber = Math.Floor(someRandomDouble) + .5;
            var roundedRight = ConsoleMath.Round(someRandomDouble);
            var roundedLame = (int)Math.Round(someRandomDouble);

            if (someRandomDouble < midPointForThisNumber)
            {
                Assert.AreEqual(Math.Floor(someRandomDouble), roundedRight);
            }
            else
            {
                Assert.AreEqual(Math.Ceiling(someRandomDouble), roundedRight);
            }

            if (roundedRight != roundedLame)
            {
                disagreed++;
            }
            else
            {
                agreed++;
            }
        }
        Assert.IsTrue(disagreed > 0);
        Console.WriteLine($"The 2 rounding modes agreed {agreed} times and disagreed {disagreed} times");
    }

    [TestMethod]
    public void ConsoleMath_Normalization()
    {
        Assert.AreEqual(1, ConsoleMath.NormalizeQuantity(1, 0));
        Assert.IsTrue(ConsoleMath.NormalizeQuantity(1, 45) < 1 && ConsoleMath.NormalizeQuantity(1, 45) > .5f);
        Assert.AreEqual(.5f, ConsoleMath.NormalizeQuantity(1, 90));

        Assert.AreEqual(1, ConsoleMath.NormalizeQuantity(1, 0, reverse: true));
        Assert.IsTrue(ConsoleMath.NormalizeQuantity(1, 45, reverse: true) > 1 && ConsoleMath.NormalizeQuantity(1, 45, reverse: true) < 2f);
        Assert.AreEqual(2f, ConsoleMath.NormalizeQuantity(1, 90, reverse: true));
    }

    [TestMethod]
    public void NormalizeQuantity_CanonicalAngles()
    {
        // No change at 0 and 180
        Assert.AreEqual(10f, ConsoleMath.NormalizeQuantity(10, 0), 0.0001f);
        Assert.AreEqual(10f, ConsoleMath.NormalizeQuantity(10, 180), 0.0001f);

        // Halved at 90 and 270
        Assert.AreEqual(5f, ConsoleMath.NormalizeQuantity(10, 90), 0.0001f);
        Assert.AreEqual(5f, ConsoleMath.NormalizeQuantity(10, 270), 0.0001f);

        // Symmetry: NQ(x, theta) == NQ(x, 360-theta)
        float v1 = ConsoleMath.NormalizeQuantity(10, 30);
        float v2 = ConsoleMath.NormalizeQuantity(10, 330);
        Assert.AreEqual(v1, v2, 0.0001f);

        // Check monotonicity: 0 -> 90 halves, so values decrease smoothly
        float prev = 10;
        for (int angle = 0; angle <= 90; angle += 10)
        {
            float norm = ConsoleMath.NormalizeQuantity(10, angle);
            Assert.IsTrue(norm <= prev, $"Angle {angle}: {norm} > {prev}");
            prev = norm;
        }
    }

    [TestMethod]
    public void NormalizeQuantity_IsSymmetricAbout180()
    {
        // 0 == 360, 10 == 350, etc.
        for (int angle = 0; angle <= 180; angle += 10)
        {
            float n1 = ConsoleMath.NormalizeQuantity(10, angle);
            float n2 = ConsoleMath.NormalizeQuantity(10, 360 - angle);
            Assert.AreEqual(n1, n2, 0.0001f, $"Failed at {angle} vs {360 - angle}");
        }
    }

    [TestMethod]
    public void RadialOffset_ShouldDrawPerfectVisualCircle()
    {
        var center = new LocF(0, 0);
        float radius = 40;
        var tolerance = 0.7f;

        foreach (var angle in Angle.Enumerate360Angles(0, 10))
        {
            var point = center.RadialOffset(angle, radius);
            float actual = point.CalculateNormalizedDistanceTo(center);
            Assert.IsTrue(Math.Abs(actual - radius) < tolerance,
                $"Angle {angle.Value}: normalized distance {actual} != {radius}");
        }
    }

    [TestMethod]
    public void RadialOffset_RoundedPositions_AreEvenlyDistributed()
    {
        var center = new LocF(0, 0);
        float radius = 40;
        int angleStep = 5; // finer resolution for smoothness

        // Dictionary: (rounded X,Y) -> count of times hit
        var cellHits = new Dictionary<(int X, int Y), int>();
        var angleHits = new Dictionary<(int X, int Y), List<float>>(); // to track angles hitting same cell

        foreach (var angle in Angle.Enumerate360Angles(0, angleStep))
        {
            var point = center.RadialOffset(angle, radius);
            var rounded = point.GetRounded();
            var key = ((int)rounded.Left, (int)rounded.Top);

            if (!cellHits.ContainsKey(key))
            {
                cellHits[key] = 0;
                angleHits[key] = new List<float>();
            }
            cellHits[key]++;
            angleHits[key].Add(angle.Value);
        }

        // Print all the unique cell positions and their counts
        foreach (var kvp in cellHits.OrderBy(x => x.Key.Y).ThenBy(x => x.Key.X))
        {
            var key = kvp.Key;
            var count = kvp.Value;
            var angles = string.Join(",", angleHits[key].Select(a => a.ToString("F1")));
            Console.WriteLine($"Cell ({key.X},{key.Y}) hit {count} times at angles: [{angles}]");
        }

        // Calculate summary stats
        var maxHits = cellHits.Values.Max();
        var minHits = cellHits.Values.Min();
        var avgHits = cellHits.Values.Average();
        Console.WriteLine($"Unique cells: {cellHits.Count}");
        Console.WriteLine($"Max hits for a cell: {maxHits}");
        Console.WriteLine($"Min hits for a cell: {minHits}");
        Console.WriteLine($"Avg hits per cell: {avgHits:F2}");

        // Simple assertion: should not have any cell with, say, >3 hits (tune as needed)
        Assert.IsTrue(maxHits <= 3, "Some cells are hit by too many angles—possible quantization artifact.");
    }


}

