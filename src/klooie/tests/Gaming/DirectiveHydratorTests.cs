using klooie.Gaming.Code;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System.Reflection;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Code)]
public class DirectiveHydratorTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        // since entry assembly is not this
        DirectiveHydrator.DirectiveSources.Add(Assembly.GetExecutingAssembly());
    }

    [TestMethod]
    public void DirectiveHydrator_Basic()
    {
        var hydrated = DirectiveHydrator.Hydrate("//#Test -IntArgument 5") as TestDirective;
        Assert.IsNotNull(hydrated);
        Assert.AreEqual(5, hydrated.IntArgument);

        try
        {
            DirectiveHydrator.Hydrate("//#Test");
            Assert.Fail("An exception should have been thrown");
        }
        catch(MissingArgException)
        {

        }

        var commandified = DirectiveHydrator.Commandify("//#Test -IntArgument 5");
        Assert.AreEqual("Test", commandified[0]);
        Assert.AreEqual("-IntArgument", commandified[1]);
        Assert.AreEqual("5", commandified[2]);
    }


    public class TestDirective : Directive
    {
        [ArgRequired]
        public int IntArgument { get; set; }
    }
}

