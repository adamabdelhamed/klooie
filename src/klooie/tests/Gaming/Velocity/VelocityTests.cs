using klooie.Gaming;
using klooie.Gaming.Code;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class VelocityTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        ConsoleProvider.Current = new KlooieTestConsole()
        {
            BufferWidth = 80,
            WindowWidth = 80,
            WindowHeight = 51
        };
    }

    [TestMethod]
    public void Velocity_MinEvalTime() => GamingTest.Run(new GamingTestOptions()
    {
        Test = async(context)=>
        {
            using (var testLt = new Lifetime())
            {
                var v = new Velocity(new ConsoleControl(), new ColliderGroup(testLt));
                for (var s = 0; s < 200; s += 5)
                {
                    v.Speed = s;
                    Console.WriteLine($"Speed = {s}, EvalFrequency = {v.EvalFreqnencySeconds} s");
                }
            }
            Game.Current.Stop();
        },
        Mode = UITestMode.Headless,
    });
}

