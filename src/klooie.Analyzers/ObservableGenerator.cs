using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace klooie
{
    [Generator]
    public class ObservableGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;

            var iObservableObjectSymbol = compilation.GetTypeByMetadataName("klooie.IObservableObject");

            var classes = compilation.SyntaxTrees
                .SelectMany(st => st.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .ToList();

            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                if (classSymbol == null || !classSymbol.IsPartial()) continue;
                if (!classSymbol.AllInterfaces.Any(i => i.Equals(iObservableObjectSymbol, SymbolEqualityComparer.Default))) continue;

                var source = ProcessClass(classSymbol);
                context.AddSource($"{classSymbol.Name}_ObservableProperties.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private string ProcessClass(INamedTypeSymbol classSymbol)
        {
            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
            var className = classSymbol.Name;
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using klooie;");

            if (namespaceName != null)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"    public partial class {className} : IObservableObject");
            sb.AppendLine("    {");

            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"        private Event _{propertyName}Changed;");
                sb.AppendLine($"        public Event {propertyName}Changed => _{propertyName}Changed ?? (_{propertyName}Changed = new Event());");
            }

            sb.AppendLine();

            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                var propertyType = property.Type.ToDisplayString();
                var fieldName = $"_{char.ToLower(propertyName[0])}{propertyName.Substring(1)}";

                sb.AppendLine($"        private {propertyType} {fieldName};");
                sb.AppendLine($"        public partial {propertyType} {propertyName}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => {fieldName};");
                sb.AppendLine("            set");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (EqualsSafe(value, {fieldName})) return;");
                sb.AppendLine($"                {fieldName} = value;");
                sb.AppendLine($"                _{propertyName}Changed?.Fire();");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("        public void Subscribe(string propertyName, Action handler, ILifetimeManager lifetimeManager)");
            sb.AppendLine("        {");
            sb.AppendLine("            var eventField = GetType().GetField($\"_{propertyName}Changed\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);");
            sb.AppendLine("            if (eventField == null) throw new ArgumentException($\"No event found for property {propertyName}\");");
            sb.AppendLine("            var eventInstance = (Event)eventField.GetValue(this);");
            sb.AppendLine("            eventInstance.Subscribe(handler, lifetimeManager);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Sync(string propertyName, Action handler, ILifetimeManager lifetimeManager)");
            sb.AppendLine("        {");
            sb.AppendLine("            Subscribe(propertyName, handler, lifetimeManager);");
            sb.AppendLine("            handler();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public ILifetimeManager GetPropertyValueLifetime(string propertyName)");
            sb.AppendLine("        {");
            sb.AppendLine("            var eventProperty = GetType().GetProperty($\"{propertyName}Changed\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);");
            sb.AppendLine("            if (eventProperty == null) throw new ArgumentException($\"No event found for property {propertyName}\");");
            sb.AppendLine("            var eventInstance = (Event)eventProperty.GetValue(this);");
            sb.AppendLine("            var lifetime = new Lifetime();");
            sb.AppendLine("            eventInstance.SubscribeOnce(() => lifetime.Dispose());");
            sb.AppendLine("            return lifetime.Manager;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static bool EqualsSafe(object a, object b)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (a == null && b == null) return true;");
            sb.AppendLine("            if (a == null ^ b == null) return false;");
            sb.AppendLine("            if (ReferenceEquals(a, b)) return true;");
            sb.AppendLine("            return a.Equals(b);");
            sb.AppendLine("        }");

            sb.AppendLine("    }");

            if (namespaceName != null)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
    }

    public static class PropertySymbolExtensions
    {
        public static bool IsPartial(this IPropertySymbol propertySymbol)
        {
            return propertySymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax() as PropertyDeclarationSyntax)
                .Any(syntax => syntax?.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)) == true);
        }

        public static bool IsPartial(this INamedTypeSymbol propertySymbol)
        {
            return propertySymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax() as ClassDeclarationSyntax)
                .Any(syntax => syntax?.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)) == true);
        }

        public static bool IsAutoProperty(this IPropertySymbol propertySymbol)
        {
            return propertySymbol.GetMethod?.DeclaringSyntaxReferences
                       .All(reference => reference.GetSyntax() is AccessorDeclarationSyntax accessor &&
                                         accessor.Body == null &&
                                         accessor.ExpressionBody == null) == true &&
                   propertySymbol.SetMethod?.DeclaringSyntaxReferences
                       .All(reference => reference.GetSyntax() is AccessorDeclarationSyntax accessor &&
                                         accessor.Body == null &&
                                         accessor.ExpressionBody == null) == true;
        }
    }
}
