using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace klooie
{
    [Generator]
    public class RecyclablePoolGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {

            // Find all class declarations
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(static c => c != null);

            // Combine with the compilation for semantic analysis
            var compilationProvider = context.CompilationProvider;

            var recyclableClassInfos = classDeclarations
                .Combine(compilationProvider)
                .Select((tuple, _) =>
                {
                    var (classDeclaration, compilation) = tuple;

                    var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                    var recyclableClassSymbol = compilation.GetTypeByMetadataName("klooie.Recyclable");
                    if (classSymbol == null || recyclableClassSymbol == null)
                        return null;

                    // Filtering logic as before
                    if (classSymbol.IsAbstract) return null;
                    if (classSymbol.DeclaredAccessibility != Accessibility.Public) return null;
                    if (classSymbol.TypeParameters.Any()) return null;
                    if (classSymbol.ContainingType != null) return null;
                    if (!IsEqualsOrDerivesFromBaseType(classSymbol, recyclableClassSymbol)) return null;
                    if (!classSymbol.Constructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)) return null;

                    return classSymbol;
                })
                .Where(static s => s != null);

            context.RegisterSourceOutput(recyclableClassInfos, (spc, classSymbol) =>
            {
                var source = GeneratePoolClass(classSymbol!);
                spc.AddSource($"{classSymbol!.Name}Pool.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }

        private static bool IsEqualsOrDerivesFromBaseType(INamedTypeSymbol classSymbol, INamedTypeSymbol baseTypeSymbol)
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

        private static string GeneratePoolClass(INamedTypeSymbol classSymbol)
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
