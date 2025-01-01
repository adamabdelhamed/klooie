using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;

namespace klooie
{
    [Generator]
    public class RecyclablePoolGenerator : ISourceGenerator
    {
        private INamedTypeSymbol recyclableInterfaceSymbol;

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            recyclableInterfaceSymbol = compilation.GetTypeByMetadataName("klooie.IRecyclable");

            var classes = compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .ToList();

            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                if (classSymbol == null || classSymbol.IsAbstract) continue;
                if (!classSymbol.AllInterfaces.Any(i => i.Equals(recyclableInterfaceSymbol, SymbolEqualityComparer.Default))) continue;
                if (!classSymbol.Constructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)) continue;

                var source = GeneratePoolClass(classSymbol);
                context.AddSource($"{classSymbol.Name}Pool.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private string GeneratePoolClass(INamedTypeSymbol classSymbol)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using klooie;");

            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();

            if (namespaceName != null)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"public class {classSymbol.Name}Pool : RecycleablePool<{classSymbol.Name}>");
            sb.AppendLine("{");
            sb.AppendLine($"    private static {classSymbol.Name}Pool? _instance;");
            sb.AppendLine($"    public static {classSymbol.Name}Pool Instance => _instance ??= new {classSymbol.Name}Pool();");
            sb.AppendLine($"    public override {classSymbol.Name} Factory() => new {classSymbol.Name}();");
            sb.AppendLine("}");

            if (namespaceName != null)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
    }
}
