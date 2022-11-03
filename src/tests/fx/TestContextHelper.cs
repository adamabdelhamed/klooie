using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace klooie.tests;

public static class TestContextHelper
{
    public static string TestId(this TestContext context) => context == null ? "Unknown" : $"{context.FullyQualifiedTestClassName}.{context.TestName}";
}
