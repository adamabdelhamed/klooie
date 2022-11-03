using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class AngleTests
{
    [TestMethod]
    public void Angles_Sanity()
    {
        for(var i = -720f; i <= 720f; i++)
        {
            TestConvertability(i);
            TestEquality(i, i + 360);
            TestEquality(i, i - 360);
            TestOpposite(i, i + 180);
            TestOpposite(i, i - 180);

            var a = new Angle(i);
            Assert.IsTrue(a.Value >= 0);
            Assert.IsTrue(a.Value < 360);
        }
    }

    [TestMethod]
    public void Angles_Add()
    {
        var a = new Angle(360);
        var b = a.Add(90);
        Assert.AreEqual(90, b.Value);

        var c = new Angle(360);
        var d = c.Add(-90);
        Assert.AreEqual(270, d.Value);
    }

    [TestMethod]
    public void Angles_Diff()
    {
        for (var i = -720f; i <= 720f; i++)
        {
            Assert.AreEqual(1, new Angle(i).DiffShortest(i+1));
            Assert.AreEqual(1, new Angle(i).DiffShortest(i-1));
            Assert.AreEqual(1, new Angle(i).DiffClockwise(i+1));
            Assert.AreEqual(359, new Angle(i).DiffCounterClockwise(i+1));
            Assert.AreEqual(359, new Angle(i).DiffClockwise(i - 1));
            Assert.AreEqual(1, new Angle(i).DiffCounterClockwise(i - 1));
        }
    }

    [TestMethod]
    public void Angles_Rounding()
    {
        Assert.AreEqual(0, Angle.Right.Value);
        Assert.AreEqual(90, Angle.Down.Value);
        Assert.AreEqual(180, Angle.Left.Value);
        Assert.AreEqual(270, Angle.Up.Value);

        for (var i = -45; i < -45+90; i++)
        {
            Assert.AreEqual(0, new Angle(i).RoundAngleToNearest(90).Value);
        }

        for (var i = 45; i < 45+90; i++)
        {
            Assert.AreEqual(90, new Angle(i).RoundAngleToNearest(90).Value);
        }

        for (var i = 135; i < 135+90; i++)
        {
            Assert.AreEqual(180, new Angle(i).RoundAngleToNearest(90).Value);
        }

        for (var i = 225; i < 225 + 90; i++)
        {
            Assert.AreEqual(270, new Angle(i).RoundAngleToNearest(90).Value);
        }
    }

    [TestMethod]
    public void Angles_ShortestPath()
    {
        for (var i = -720f; i <= 720f; i++)
        {
            var a = new Angle(i);
            Assert.IsTrue(a.IsClockwiseShortestPathToAngle(new Angle(i + 179)));
            Assert.IsTrue(a.IsClockwiseShortestPathToAngle(new Angle(i + 180)));
            Assert.IsFalse(a.IsClockwiseShortestPathToAngle(new Angle(i + 181)));
        }
    }

    [TestMethod]
    public void Angles_Bisect()
    {
        Assert.AreEqual(45, new Angle(0).Bisect(90, true));
        Assert.AreEqual(0, new Angle(359).Bisect(1, true));
        Assert.AreEqual(90, new Angle(0).Bisect(180, true));
        Assert.AreEqual(270, new Angle(181).Bisect(359, true));
        Assert.AreEqual(270, new Angle(180).Bisect(360, true));
        Assert.AreEqual(90, new Angle(180).Bisect(360, false));
    }

    private void TestOpposite(float a, float b)
    {
        Angle aa = new Angle(a);
        Angle ba = new Angle(b);
        Assert.IsTrue(aa.Opposite() == ba);
        Assert.IsTrue(aa.Opposite() != aa);
    }

    private void TestEquality(float a, float b)
    {
        Angle aa = new Angle(a);
        Angle ba = new Angle(b);
        Assert.AreEqual(aa, ba);
        Assert.AreEqual(aa.Value, ba.Value);
    }
 

    private void TestConvertability(float angleVal)
    {
        Angle a = new Angle(angleVal);
        Angle b = angleVal;
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.Value, b.Value);
    }
}

