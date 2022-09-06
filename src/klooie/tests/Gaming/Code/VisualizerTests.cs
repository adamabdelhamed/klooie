using klooie.Gaming;
using klooie.Gaming.Code;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System.Linq;
using System.Reflection;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Code)]
public class VisualizerTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        // since entry assembly is not this
        DirectiveHydrator.DirectiveSources.Add(Assembly.GetExecutingAssembly());
    }

    [TestMethod]
    public void CodeVisualizer_Basic() => GamingTest.RunCustomSize(new ArrayRulesProvider(new IRule[0]), TestContext.TestId(),23,7, UITestMode.KeyFramesVerified, async(context)=>
    {
        var code =
@"
    function Foo()
    {
        var foo = 1;
        var bar = 2;
    }
";
        var ast = Compiler.Compile(new CompilerOptions()
        {
            Code = code,
            CodeLocation = "Test Code",
        });

        var process = new Process(Game.Current, ast);
        process.RenderCode(ast.Root, true, 0, 0);
        Game.Current.LayoutRoot.Background = RGB.Red;
        await context.PaintAndRecordKeyFrameAsync();
        Game.Current.Stop();
    });

    [TestMethod]
    public void CodeVisualizer_Run() => GamingTest.RunCustomSize(new ArrayRulesProvider(new IRule[0]), TestContext.TestId(), 23, 7, UITestMode.RealTimeFYI, async (context) =>
    {
        var code =
@"
    function Foo()
    {
        //#set foo 1 -heap -on demand
        var foo = 1;
        //#set bar 2 -heap -on demand        
        var bar = 2;
    }
";
        var ast = Compiler.Compile(new CompilerOptions() { Code = code } );
        var process = new Process(Game.Current, ast);
        process.RenderCode(ast.Root, true, 0, 0);
        Game.Current.LayoutRoot.Background = RGB.Red;
        await process.AST.Functions.Single().Execute().AsTask();
        Assert.AreEqual(1, Heap.Current.Get<int>("foo"));
        Assert.AreEqual(2, Heap.Current.Get<int>("bar"));
        Game.Current.Stop();
    });
}

