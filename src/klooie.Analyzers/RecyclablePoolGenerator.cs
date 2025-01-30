using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace klooie
{
    [Generator]
    public class RecyclablePoolGenerator : ISourceGenerator
    {
        private INamedTypeSymbol recyclableClassSymbol;

        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            recyclableClassSymbol = compilation.GetTypeByMetadataName("klooie.Recyclable");

            var classes = compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .ToList();

            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                // Ensure the type meets the criteria
                if (classSymbol == null || classSymbol.IsAbstract) continue;
                if (classSymbol.DeclaredAccessibility != Accessibility.Public) continue;
                if (classSymbol.TypeParameters.Any()) continue; // Skip if it has generic arguments
                if (classSymbol.ContainingType != null) continue; // Exclude nested types
                if (!IsEqualsOrDerivesFromBaseType(classSymbol, recyclableClassSymbol)) continue;
                if (!classSymbol.Constructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)) continue;

                var source = GeneratePoolClass(classSymbol);
                context.AddSource($"{classSymbol.Name}Pool.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private bool IsEqualsOrDerivesFromBaseType(INamedTypeSymbol classSymbol, INamedTypeSymbol baseTypeSymbol)
        {
            var currentType = classSymbol;
            while (currentType != null)
            {
                if (currentType.Equals(baseTypeSymbol, SymbolEqualityComparer.Default))
                    return true;

                currentType = currentType.BaseType;
            }
            return false;
        }

        private string GeneratePoolClass(INamedTypeSymbol classSymbol)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using klooie;");

            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
            var poolClassName = classSymbol.Name == "Recyclable" ? "DefaultRecyclablePool" : $"{classSymbol.Name}Pool";

            if (namespaceName != null)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"public class {poolClassName} : RecycleablePool<{classSymbol.Name}>");
            sb.AppendLine("{");
            sb.AppendLine($"    private static {poolClassName}? _instance;");
            sb.AppendLine($"    public static {poolClassName} Instance => _instance ??= new {poolClassName}();");
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
