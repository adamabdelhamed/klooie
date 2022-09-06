using klooie.Gaming.Code;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System.Linq;
using System.Reflection;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Code)]
public class CompilerTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        // since entry assembly is not this
        DirectiveHydrator.DirectiveSources.Add(Assembly.GetExecutingAssembly());
    }

    [TestMethod]
    public void Compiler_Basic()
    {
        var code =
@"
    //#script -id TestScript
    function Foo()
    {
        
    }

";
        var ast = Compiler.Compile(new CompilerOptions()
        {
            Code = code,
            CodeLocation = "Test Code",
            ScriptProvider = new TestScriptProvider()
        });
        Assert.AreEqual(10, (ast.Rules.Single() as TestCompilerDirective).IntArgument);
        Assert.AreEqual(1, ast.Functions.Count());
        Assert.AreEqual("Foo", ast.Functions.Single().Name);
        Assert.AreEqual(0, ast.Functions.Single().Statements.Count);
    }

    public class TestCompilerDirective : Directive
    {
        [ArgRequired]
        public int IntArgument { get; set; }
    }

    public class TestScriptProvider : IScriptProvider
    {
        public string LoadScriptById(string id)
        {
            if("TestScript".Equals(id, System.StringComparison.OrdinalIgnoreCase))
            {
                return "//#TestCompiler -IntArgument 10";
            }
            else
            {
                throw new System.Exception("No script: " + id);
            }
        }
    }
}

