
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
}

