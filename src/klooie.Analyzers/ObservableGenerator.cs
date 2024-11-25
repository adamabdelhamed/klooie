using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Diagnostics;
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
                if (classSymbol.Name.Contains("Dropdown"))
                {
                    //Debugger.Launch();                    
                }
                context.AddSource($"{classSymbol.Name}_ObservableProperties.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private string ProcessClass(INamedTypeSymbol classSymbol)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using klooie;");

            var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
            var classHierarchy = new Stack<INamedTypeSymbol>();
            var currentType = classSymbol;

            while (currentType != null)
            {
                classHierarchy.Push(currentType);
                currentType = currentType.ContainingType;
            }

            var indent = 0;
            var standardIndent = 4;
            // Open namespace if needed
            if (namespaceName != null)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
                indent += standardIndent;
            }

            // Open nested classes
            foreach (var currentClassSymbol in classHierarchy)
            {
                var baseType = currentClassSymbol.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? string.Empty;
                var interfaces = currentClassSymbol.Interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                var typeParameters = currentClassSymbol.TypeParameters.Any()
                    ? $"<{string.Join(", ", currentClassSymbol.TypeParameters.Select(tp => tp.Name))}>"
                    : string.Empty;
                var constraints = currentClassSymbol.TypeParameters
                    .Where(tp => tp.ConstraintTypes.Any())
                    .Select(tp =>
                    {
                        var constraintList = string.Join(", ", tp.ConstraintTypes.Select(ct => ct.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        return $"where {tp.Name} : {constraintList}";
                    });

                // Build base types clause (includes base type and interfaces)
                var baseTypes = new List<string>();
                if (!string.IsNullOrEmpty(baseType) && baseType != "object") baseTypes.Add(baseType);
                baseTypes.AddRange(interfaces);

                var baseTypesClause = baseTypes.Any() ? " : " + string.Join(", ", baseTypes) : string.Empty;

                // Append the class signature
                sb.AppendLine($"{Indent(indent)}{currentClassSymbol.DeclaredAccessibility.ToString().ToLower()} partial class {currentClassSymbol.Name}{typeParameters}{baseTypesClause}");

                // Append constraints, if any
                foreach (var constraint in constraints)
                {
                    sb.AppendLine($"{Indent(indent + standardIndent)}{constraint}");
                }

                sb.AppendLine(Indent(indent) + "{");
                indent += standardIndent;
            }



            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                sb.AppendLine($"{Indent(indent)}private Event _{propertyName}Changed;");
                sb.AppendLine($"{Indent(indent)}{property.DeclaredAccessibility.ToString().ToLower()} Event {propertyName}Changed => _{propertyName}Changed ?? (_{propertyName}Changed = new Event());");
            }

            sb.AppendLine();
            foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsPartial() && p.IsAutoProperty()))
            {
                var propertyName = property.Name;
                var propertyType = property.Type.ToDisplayString();
                var fieldName = $"_{char.ToLower(propertyName[0])}{propertyName.Substring(1)}";

                sb.AppendLine($"{Indent(indent)}private {propertyType} {fieldName};");
                sb.AppendLine($"{Indent(indent)}{property.DeclaredAccessibility.ToString().ToLower()} partial {propertyType} {propertyName}");
                sb.AppendLine(Indent(indent)+"{");
                indent += standardIndent;
                sb.AppendLine($"{Indent(indent)}get => {fieldName};");
                sb.AppendLine($"{Indent(indent)}set");
                sb.AppendLine(Indent(indent)+"{");
                indent += standardIndent;
                sb.AppendLine($"{Indent(indent)}if (EqualsSafe(value, {fieldName})) return;");
                sb.AppendLine($"{Indent(indent)}{fieldName} = value;");
                sb.AppendLine($"{Indent(indent)}_{propertyName}Changed?.Fire();");
                indent -= standardIndent;
                sb.AppendLine(Indent(indent)+"}");
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
                sb.AppendLine();
            }

            sb.AppendLine($"{Indent(indent)}public void Subscribe(string propertyName, Action handler, ILifetimeManager lifetimeManager)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;

            // Try to get the property and subscribe if found
            sb.AppendLine(Indent(indent) + "var eventProp = GetType().GetProperty($\"{propertyName}Changed\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);");
            sb.AppendLine(Indent(indent) + "if (eventProp != null)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "var eventInstance = (Event)eventProp.GetValue(this);");
            sb.AppendLine(Indent(indent) + "eventInstance.Subscribe(handler, lifetimeManager);");
            sb.AppendLine(Indent(indent) + "return;");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");

            // Check if the current object derives from ObservableObject
            sb.AppendLine(Indent(indent) + "if (((object)this) is ObservableObject observableObject)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "observableObject.Subscribe(propertyName, handler, lifetimeManager);");
            sb.AppendLine(Indent(indent) + "return;");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");

            // If no event and not an ObservableObject, throw an exception
            sb.AppendLine(Indent(indent) + "throw new ArgumentException($\"No event found for property {propertyName}\");");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();
            sb.AppendLine(Indent(indent) + "public void Sync(string propertyName, Action handler, ILifetimeManager lifetimeManager)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "Subscribe(propertyName, handler, lifetimeManager);");
            sb.AppendLine(Indent(indent) + "handler();");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();
            sb.AppendLine(Indent(indent) + "public ILifetimeManager GetPropertyValueLifetime(string propertyName)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "var eventProperty = GetType().GetProperty($\"{propertyName}Changed\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);");
            sb.AppendLine(Indent(indent) + "if (eventProperty == null) throw new ArgumentException($\"No event found for property {propertyName}\");");
            sb.AppendLine(Indent(indent) + "var eventInstance = (Event)eventProperty.GetValue(this);");
            sb.AppendLine(Indent(indent) + "var lifetime = new Lifetime();");
            sb.AppendLine(Indent(indent) + "eventInstance.SubscribeOnce(() => lifetime.Dispose());");
            sb.AppendLine(Indent(indent) + "return lifetime.Manager;");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");
            sb.AppendLine();
            sb.AppendLine(Indent(indent) + "private static bool EqualsSafe(object a, object b)");
            sb.AppendLine(Indent(indent) + "{");
            indent += standardIndent;
            sb.AppendLine(Indent(indent) + "if (a == null && b == null) return true;");
            sb.AppendLine(Indent(indent) + "if (a == null ^ b == null) return false;");
            sb.AppendLine(Indent(indent) + "if (ReferenceEquals(a, b)) return true;");
            sb.AppendLine(Indent(indent) + "return a.Equals(b);");
            indent -= standardIndent;
            sb.AppendLine(Indent(indent) + "}");

            // Close nested classes
            for (int i = 0; i < classHierarchy.Count; i++)
            {
                indent -= standardIndent;
                sb.AppendLine(Indent(indent) + "}");
            }

            // Close namespace if needed
            if (namespaceName != null)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string Indent(int amount) => new string(' ', amount);
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
